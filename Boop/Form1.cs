using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using System.Threading;

namespace Boop
{

	public partial class Form1 : Form
	{
		const string SWITCH = "Switch";
		const string N3DS = "3DS";
		const string NONE = "none";

		Task task;
		CancellationTokenSource cts;
		WebServer newHTTPServer;
		Socket s; //Socket to tell FBI where the server is
		string[] FilesToBoop; //Files to be boop'd
		string ActiveDir; //Used to mount the server
		string _consolemode = "none";
		string ConsoleMode
		{
			get
			{
				return _consolemode;
			}
			set
			{
				if (value == SWITCH)
				{
					_consolemode = SWITCH;
					picSplash.Image = Properties.Resources._switch;
					lblMode.Text = "Nintendo Switch";
					lblMode.ForeColor = System.Drawing.Color.FromArgb(0xe60012);

					//Do other changes.
				}
				else if (value.ToUpper() == N3DS)
				{
					_consolemode = N3DS;
					picSplash.Image = Properties.Resources._3ds;
					lblMode.Text = "Nintendo 3DS";
					lblMode.ForeColor = System.Drawing.Color.FromArgb(0x48bbff);

					//Do other changes.
				}
				else
				{
					_consolemode = NONE;
					picSplash.Image = Properties.Resources.generic;
					lblMode.Text = "";
					//reset the UI.
				}
			}
		}

		public Form1()
		{
			InitializeComponent();

			var pos = this.PointToScreen(lblImageVersion.Location);
			pos = picSplash.PointToClient(pos);
			lblImageVersion.Parent = picSplash;
			lblImageVersion.Location = pos;

			Application.ApplicationExit += new EventHandler(this.OnApplicationExit);

			//Drag and drop support
			string[] args = Environment.GetCommandLineArgs();
			if (args != null && args.Length > 0) //If drag and drop
			{
				List<String> Boops = new List<String>(); //Initialize a temporal list.
				foreach (string arg in args)
				{
					if (System.IO.File.Exists(arg)) //Is it a file?
					{
						if (Path.GetExtension(arg) == ".cia" || Path.GetExtension(arg) == ".tik" || Path.GetExtension(arg) == ".nsp") //Is it a supported file?
						{
							Boops.Add(arg); //Add it.
						}
					}
				}
				if (Boops.Count > 0) //If we had any supported file
				{
					FilesToBoop = Boops.ToArray(); //Set them
					ProcessFilenames(); //Add them to the list.
				}
			}

		}


		private void OnApplicationExit(object sender, EventArgs e)
		{
			//Individual trycatches to make sure everything is off before leaving.
			try
			{
				//Stop the webServer
				cts.Cancel();
				//task.Wait();
				newHTTPServer.Dispose();
			}
			catch { }

			try
			{
				s.Close();
			}
			catch { }

		}

		private void btnPickFiles_Click(object sender, EventArgs e)
		{
			// Create an instance of the open file dialog box.
			lblFileMarker.Visible = false;
			OpenFileDialog OFD = new OpenFileDialog();

			// Set filter options and filter index.
			OFD.Filter = "Archivos compatibles con Boop (*.nsp, *.cia, *.tik)|*.nsp;*.cia;*.tik|Tinfoil compatible files (*.nsp)|*.nsp|Archivos compatibles con FBI (*.cia, *.tik)|*.cia;*.tik";

			OFD.FilterIndex = 0;

			OFD.Multiselect = true;

			bool? userClickedOK = (OFD.ShowDialog() == DialogResult.OK);


			// Process input if the user clicked OK.
			if (userClickedOK == true)
			{
				if (OFD.FileNames.Length > 0)
				{
					lvFileList.Items.Clear();
					FilesToBoop = OFD.FileNames;
					ProcessFilenames(); // I splited this button in order to reuse the code for the drag and drop support.
				}

			}
		}

		/// <summary>
		/// Processes The Files
		/// </summary>
		private void ProcessFilenames()
		{
			ConsoleMode = NONE; //FREE FOR ALL!

			ActiveDir = (Path.GetDirectoryName(FilesToBoop[0]));

			foreach (string item in FilesToBoop)
			{
				if (ActiveDir == Path.GetDirectoryName(item))
				{
					if (ConsoleMode == NONE)
					{
						//GUEEEESS THE TYYYPE!
						if (Path.GetExtension(item) == ".cia" || Path.GetExtension(item) == ".tik") ConsoleMode = N3DS;
						if (Path.GetExtension(item) == ".nsp") ConsoleMode = SWITCH;
					}


					if (ConsoleMode == N3DS)
					{
						if (Path.GetExtension(item) == ".cia")
						{
							byte[] desc = new Byte[256];

							byte[] tit = new Byte[128];

							using (BinaryReader b = new BinaryReader(File.Open(item, FileMode.Open)))
							{
								b.BaseStream.Seek(-14016 + 520, SeekOrigin.End);
								tit = b.ReadBytes(128);

								b.BaseStream.Seek(-14016 + 520 + 128, SeekOrigin.End);
								desc = b.ReadBytes(256);
							}

							string[] tmp = new string[3];
							tmp[0] = Path.GetFileName(item);
							tmp[1] = Encoding.Unicode.GetString(tit).Trim();
							tmp[2] = Encoding.Unicode.GetString(desc).Trim();



							lvFileList.Items.Add(new ListViewItem(tmp));
						}
						else if (Path.GetExtension(item) == ".tik")
						{
							lvFileList.Items.Add(Path.GetFileName(item));
						}
					}
					else if (ConsoleMode == SWITCH)
					{
						if (Path.GetExtension(item) == ".nsp")
						{
							lvFileList.Items.Add(Path.GetFileName(item));
							//try to get the filename and description!
							/*
							string[] tmp = new string[3];
							tmp[0] = Path.GetFileName(item);
							tmp[1] = Encoding.Unicode.GetString(tit).Trim();
							tmp[2] = Encoding.Unicode.GetString(desc).Trim();
							*/
						}
					}
				}
				else
				{
					MessageBox.Show("Escogiste 2 archivos de diferentes directorios" + Environment.NewLine + "Para boopear directorios cruzados se necesita hostear todo el PC, cosa que a día de hoy no soy capaz de programar." + Environment.NewLine + "En el futuro posiblemente se implemente.", "Vaya...", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}

			}
		}

		private void btnBoop_Click(object sender, EventArgs e)
		{
			//Try catch will go away in the future. Left in case somebody still has trouble with the server.

			//Reset all red markers.
			lblFileMarker.Visible = false;
			lblIPMarker.Visible = false;
			lblPortMarker.Visible = false;

			if (NetUtil.IPv4.iIPIndex == -1)
			{
				MessageBox.Show("Tu PC no está conectado a una red" + Environment.NewLine + "Si quieres conectarlo después de abrir Boop-ES, reinicie el programa", "No se detectó una red local", MessageBoxButtons.OK, MessageBoxIcon.Error);
				//Added red boxes to point out the errors.
				return;
			}

			try
			{
				//#endif

				//Fastest check first.
				if (lvFileList.Items.Count == 0)
				{
					MessageBox.Show("Añade algunos archivos", "No se han encontrado archivos para boopear", MessageBoxButtons.OK, MessageBoxIcon.Error);
					lblFileMarker.Visible = true; //Added red boxes to point out the errors.
					return;
				}

				if (NetUtil.IPv4.ValidatePort(txtPort.Text) == false)
				{
					MessageBox.Show("Eso no parece un puerto." + Environment.NewLine + "Un puerto es por ejemplo: 8080", "Error en el número del puerto", MessageBoxButtons.OK, MessageBoxIcon.Error);
					lblPortMarker.Visible = true; //Added red boxes to point out the errors.
					setStatusLabel("Ready");
					return;
				}

				if (NetUtil.IPv4.Validate(txtConsole.Text) == false)
				{
					MessageBox.Show("Eso no parece una IP." + Environment.NewLine + "Una IP es por ejemplo: 192.168.1.6" + Environment.NewLine + "(Es decir: Números.Números.Números.Números)", "Error en la IP", MessageBoxButtons.OK, MessageBoxIcon.Error);
					lblIPMarker.Visible = true; //Added red boxes to point out the errors.
					setStatusLabel("");
					return;
				}

				string sConsoleIP = txtConsole.Text;
				int iLocalPort = int.Parse(txtPort.Text);

				int iConsolePort = 5000;

				if (ConsoleMode == SWITCH) iConsolePort = 2000;
				if (ConsoleMode == N3DS) iConsolePort = 5000;

				if (NetUtil.IPv4.PortInUse(iLocalPort) || iLocalPort == iConsolePort)
				{
					MessageBox.Show("Este puerto ya está en uso." + Environment.NewLine + "", "Error en el número del puerto", MessageBoxButtons.OK, MessageBoxIcon.Error);
					lblPortMarker.Visible = true; //Added red boxes to point out the errors.
					setStatusLabel("");
					return;
				}


				setStatusLabel("Abriendo el servidor sneck...");
				enableControls(false);

				newHTTPServer = WebServer
				.Create("http://"+ NetUtil.IPv4.Local + ":"+iLocalPort+"/")
				.WithStaticFolderAt(ActiveDir);

				cts = new CancellationTokenSource();
				task = newHTTPServer.RunAsync(cts.Token);

				Thread.Sleep(100);

				setStatusLabel("Abriendo el socket para enviar el archivo listado...");

				s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

				IAsyncResult result = s.BeginConnect(sConsoleIP, iConsolePort, null, null);

				result.AsyncWaitHandle.WaitOne(5000, true);

				if (!s.Connected)
				{
					s.Close();

					//Stop the webServer
					cts.Cancel();
					//task.Wait();
					newHTTPServer.Dispose();

					MessageBox.Show("Fallo al conectar a la consola", "Conexión fallida", MessageBoxButtons.OK, MessageBoxIcon.Error);
					lblIPMarker.Visible = true;
					setStatusLabel("");
					enableControls(true);
					return;
				}

				setStatusLabel("Enviado el archivo enviado...");

				String message = "";

				foreach (var file in FilesToBoop)
				{
					message += NetUtil.IPv4.Local + ":"+txtPort.Text+"/" + Uri.EscapeDataString(Path.GetFileName(file)) + "\n";
				}

				//boop the info to the console...
				byte[] Largo = BitConverter.GetBytes((uint)Encoding.ASCII.GetBytes(message).Length);
				byte[] Adress = Encoding.ASCII.GetBytes(message);

				Array.Reverse(Largo); //Endian fix

				s.Send(AppendTwoByteArrays(Largo, Adress));

				setStatusLabel("Boopeando archivos... Espere.");
				s.BeginReceive(new byte[1], 0, 1, 0, new AsyncCallback(GotData), null); //Call me back when the 3ds says something.

				//#if DEBUG
			}
			catch (Exception ex)
			{
				//Hopefully, some day we can have all the different exceptions handled... One can dream, right? *-*
				MessageBox.Show("Algo salió mal: " + Environment.NewLine + Environment.NewLine + "\"" + ex.Message + "\"" + Environment.NewLine + Environment.NewLine + "Si esto sigue pasando, reporta el fallo." + Environment.NewLine + Environment.NewLine + "El programa se cerrará", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}
			//#endif
		}

		private void GotData(IAsyncResult ar)
		{

			// now we unlock the controls...
			//Spooky "thread safe" way to access UI from ASYNC.
			this.Invoke((MethodInvoker)delegate
			{
				enableControls(true);
				setStatusLabel("Boopeo completado.");
				System.Media.SystemSounds.Beep.Play(); //beep boop son.
				//No more annoy message.
				//MessageBox.Show("Booping complete!", "Yay!", MessageBoxButtons.OK, MessageBoxIcon.Information);
			});

			s.Close();
			//Stop the webServer
			cts.Cancel();
			//task.Wait();
			newHTTPServer.Dispose();
		}

		static byte[] AppendTwoByteArrays(byte[] arrayA, byte[] arrayB) //Aux function to append the 2 byte arrays.
		{
			byte[] outputBytes = new byte[arrayA.Length + arrayB.Length];
			Buffer.BlockCopy(arrayA, 0, outputBytes, 0, arrayA.Length);
			Buffer.BlockCopy(arrayB, 0, outputBytes, arrayA.Length, arrayB.Length);
			return outputBytes;
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			cboLocalIP.DataSource = Dns.GetHostEntry(Dns.GetHostName()).AddressList.DefaultIfEmpty(IPAddress.Loopback).Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).Select(ip => ip.ToString()).ToArray();

			lblImageVersion.Text = Utils.GetCurrentVersion();
			this.Text = "Boop-ES // Robichani6-1 + JustSofter";
			txtConsole.Text = NetUtil.IPv4.GetFirstNintendoIP() == "" ? Properties.Settings.Default["savedIP"].ToString() : NetUtil.IPv4.GetFirstNintendoIP();
			txtPort.Text = Properties.Settings.Default["savedPort"].ToString();
		}

		private void Form1_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Copy;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}


		private void Form1_DragDrop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{

				List<String> Boops = new List<String>(); //Initialize a temporal list.

				string[] filePaths = (string[])(e.Data.GetData(DataFormats.FileDrop));
				foreach (string arg in filePaths)
				{

					if (System.IO.File.Exists(arg)) //Is it a file?
					{
						if (Path.GetExtension(arg) == ".cia" || Path.GetExtension(arg) == ".tik") //Is it a supported file?
						{
							Boops.Add(arg); //Add it.
						}
					}

				}

				if (Boops.Count > 0) //If we had any supported file
				{
					lvFileList.Items.Clear();
					FilesToBoop = Boops.ToArray(); //Set them
					ProcessFilenames(); //Add them to the list.
				}

			}
		}

		private void enableControls(bool enabled)
		{
			btnBoop.Enabled = enabled;
			btnPickFiles.Enabled = enabled;
			picSplash.Enabled = enabled;
		}

		private void setStatusLabel(String text)
		{
			StatusLabel.Text = text;
			//Force-update text to appear. If we still crash from #9 we should get where it crashed.
			statusStrip1.Invalidate();
			statusStrip1.Refresh();
		}

		private String saveIPAddress(String newIPAddress)
		{
			newIPAddress = newIPAddress.Trim();
			if (NetUtil.IPv4.Validate(newIPAddress))
			{
				Properties.Settings.Default["savedIP"] = newIPAddress;
				Properties.Settings.Default.Save();
			}
			return newIPAddress;
		}

		private string savePortNumber(String newPortNumber)
		{
			newPortNumber = newPortNumber.Trim();
			if (NetUtil.IPv4.ValidatePort(newPortNumber))
			{
				Properties.Settings.Default["savedPort"] = newPortNumber;
				Properties.Settings.Default.Save();
			}
			return newPortNumber;
		}

		private void txt3DS_Leave(object sender, EventArgs e)
		{
			txtConsole.Text = saveIPAddress(txtConsole.Text);
		}

		private void txt3DS_TextChanged(object sender, EventArgs e)
		{
			saveIPAddress(txtConsole.Text);
			lblIPMarker.Visible = false;
		}

		private void txtPort_TextChanged(object sender, EventArgs e)
		{
			savePortNumber(txtPort.Text);
			lblPortMarker.Visible = false;
		}

		private void linkWhat_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) //Added help picture to find IP adress.
		{
			MessageBox.Show("Una IP es una etiqueta numérica asignada a cada dispositivo conectado a una red para la comunicación." + Environment.NewLine + "Te estarás preguntando '¿Cómo se ve una IP?'" + Environment.NewLine + "Se parece a esto: X.X.X.X." + Environment.NewLine + "Obténla de tu consola en la pantalla de 'Instalación Remota'.", "Explicarlo es difícil", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void btnGithub_Click(object sender, EventArgs e) //New cooler github button
		{
			Process.Start(@"https://github.com/Robichani6-1/Boop-ES");
		}

		private void btnInfo_Click(object sender, EventArgs e) //New super cool snek about form
		{
			InfoBox frmInfo = new InfoBox();
			frmInfo.ShowDialog();
		}

		private void lvFileList_SelectedIndexChanged(object sender, EventArgs e)
		{
			//Pls no touching the snek list.
			lvFileList.SelectedIndices.Clear();
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			NetUtil.IPv4.iIPIndex = cboLocalIP.SelectedIndex;
		}

		private void lblPCIP_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			MessageBox.Show("Para abrir el servidor para todas las redes se necesitaba permiso de administrador, así que decidí que solo se abriera en esa dirección." + Environment.NewLine + "Elige la primera IP y la mayoría de las veces toma la correcta." + Environment.NewLine + "Si estás conectado a más de una red asegúrese de que su IP sea correcta.", "¿Vives en una cueva?", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			MessageBox.Show("Selecciona un puerto vacío de tu ordenador" + Environment.NewLine + "Como por ejemplo 8080, 8008 y 591", "¿Estás en una cueva, tío?", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void label2_Click(object sender, EventArgs e)
		{

		}

		private void label1_Click(object sender, EventArgs e)
		{

		}

		private void lblImageVersion_Click(object sender, EventArgs e)
		{

		}

		private void picSplash_Click(object sender, EventArgs e)
		{

		}
	}
}
