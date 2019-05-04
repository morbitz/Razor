﻿using Assistant.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CUO_API;
using System.Reflection;

namespace Assistant
{
    public partial class Engine
    {
        public static unsafe void Install(PluginHeader* plugin)
        {
            Client.Init(false);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                string[] fields = e.Name.Split(',');
                string name = fields[0];
                string culture = fields[2];

                if (name.EndsWith(".resources") && !culture.EndsWith("neutral"))
                {
                    return null;
                }
                AssemblyName askedassembly = new AssemblyName(e.Name);

                bool isdll = File.Exists(Path.Combine(RootPath, askedassembly.Name + ".dll"));

                return Assembly.LoadFile(Path.Combine(RootPath, askedassembly.Name + (isdll ? ".dll" : ".exe")));

            };

            //ClientVersion = (ClientVersions)plugin->ClientVersion;

            if (!(Client.Instance as ClassicUOClient).Install(plugin))
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            string clientPath = ((OnGetUOFilePath)Marshal.GetDelegateForFunctionPointer(plugin->GetUOFilePath, typeof(OnGetUOFilePath)))();
            // Thread t = new Thread(() =>
            // {
            m_Running = true;
            //  Thread.CurrentThread.Name = "Razor Main Thread";

#if !DEBUG
			    AppDomain.CurrentDomain.UnhandledException +=
                    new UnhandledExceptionEventHandler( CurrentDomain_UnhandledException );
#endif
            Ultima.Files.SetMulPath(clientPath);
            Ultima.Multis.PostHSFormat = UsePostHSChanges;

            if (!Language.Load("ENU"))
            {
                MessageBox.Show(
                    "Fatal Error: Unable to load required file Language/Razor_lang.enu\nRazor cannot continue.",
                    "No ENU Language Pack", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            string defLang = Config.GetAppSetting<string>("DefaultLanguage");
            if (defLang != null && !Language.Load(defLang))
                MessageBox.Show(
                    String.Format(
                        "WARNING: Razor was unable to load the file Language/Razor_lang.{0}\nENU will be used instead.",
                        defLang), "Language ENU Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);


            Language.LoadCliLoc();

            Initialize(typeof(Assistant.Engine).Assembly); //Assembly.GetExecutingAssembly()

            Config.LoadCharList();
            if (!Config.LoadLastProfile())
                MessageBox.Show(
                    "The selected profile could not be loaded, using default instead.", "Profile Load Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            m_MainWnd = new MainForm();
            m_MainWnd.Show();
            /*  Application.Run(m_MainWnd);
              m_Running = false;

              Counter.Save();
              Macros.MacroManager.Save();
              Config.Save();
          });
          t.SetApartmentState(ApartmentState.STA);
          t.IsBackground = true;
          t.Start();*/
        }
    }

    public class ClassicUOClient : Client
    {
        public override DateTime ConnectionStart => m_ConnectionStart;
        public override IPAddress LastConnection { get; }
        public override Process ClientProcess => m_ClientProcess;
        public override bool ClientRunning => m_ClientRunning;
        private uint m_In, m_Out;


        private DateTime m_ConnectionStart;
        private Process m_ClientProcess = null;
        private bool m_ClientRunning = false;
        private string m_ClientVersion;
        
        private static OnPacketSendRecv _sendToClient, _sendToServer, _recv, _send;
        private static OnGetPacketLength _getPacketLength;
        private static OnGetPlayerPosition _getPlayerPosition;
        private static OnCastSpell _castSpell;
        private static OnGetStaticImage _getStaticImage;
        private static OnTick _tick;
        private static RequestMove _requestMove;
        private static OnSetTitle _setTitle;


        private static OnHotkey _onHotkeyPressed;
        private static OnMouse _onMouse;
        private static OnUpdatePlayerPosition _onUpdatePlayerPosition;
        private static OnClientClose _onClientClose;
        private static OnInitialize _onInitialize;
        private static OnConnected _onConnected;
        private static OnDisconnected _onDisconnected;
        private static OnFocusGained _onFocusGained;
        private static OnFocusLost _onFocusLost;
        private IntPtr m_ClientWindow;

        public override void SetMapWndHandle(Form mapWnd)
        {

        }

        public override void RequestStatbarPatch(bool preAOS)
        {

        }

        public override void SetCustomNotoHue(int hue)
        {

        }

        public override void SetSmartCPU(bool enabled)
        {

        }

        public override void SetGameSize(int x, int y)
        {

        }

        public override Loader_Error LaunchClient(string client)
        {
            return Loader_Error.SUCCESS;
        }

        public override bool ClientEncrypted { get; set; }

        public override bool ServerEncrypted { get; set; }

        public unsafe bool Install( PluginHeader* header)
        {
            _sendToClient = (OnPacketSendRecv)Marshal.GetDelegateForFunctionPointer( header->Recv, typeof( OnPacketSendRecv ) );
            _sendToServer = (OnPacketSendRecv)Marshal.GetDelegateForFunctionPointer( header->Send, typeof( OnPacketSendRecv ) );
            _getPacketLength = (OnGetPacketLength)Marshal.GetDelegateForFunctionPointer( header->GetPacketLength, typeof( OnGetPacketLength ) );
            _getPlayerPosition = (OnGetPlayerPosition)Marshal.GetDelegateForFunctionPointer( header->GetPlayerPosition, typeof( OnGetPlayerPosition ) );
            _castSpell = (OnCastSpell)Marshal.GetDelegateForFunctionPointer( header->CastSpell, typeof( OnCastSpell ) );
            _getStaticImage = (OnGetStaticImage)Marshal.GetDelegateForFunctionPointer( header->GetStaticImage, typeof( OnGetStaticImage ) );
            _requestMove = (RequestMove)Marshal.GetDelegateForFunctionPointer( header->RequestMove, typeof( RequestMove ) );
            _setTitle = (OnSetTitle)Marshal.GetDelegateForFunctionPointer( header->SetTitle, typeof( OnSetTitle ) );
            m_ClientVersion = new Version( (byte)(header->ClientVersion >> 24), (byte)(header->ClientVersion >> 16), (byte)(header->ClientVersion >> 8), (byte)header->ClientVersion ).ToString();
            m_ClientRunning = true;
            m_ClientWindow = header->HWND;
            _tick = Tick;
            _recv = OnRecv;
            _send = OnSend;
            _onHotkeyPressed = OnHotKeyHandler;
            _onMouse = OnMouseHandler;
            _onUpdatePlayerPosition = OnPlayerPositionChanged;
            _onClientClose = OnClientClosing;
            _onInitialize = OnInitialize;
            _onConnected = OnConnected;
            _onDisconnected = OnDisconnected;
            //_onFocusGained = OnFocusGained;
            //_onFocusLost = OnFocusLost;
            header->Tick = Marshal.GetFunctionPointerForDelegate( _tick );
            header->OnRecv = Marshal.GetFunctionPointerForDelegate( _recv );
            header->OnSend = Marshal.GetFunctionPointerForDelegate( _send );
            header->OnHotkeyPressed = Marshal.GetFunctionPointerForDelegate( _onHotkeyPressed );
            header->OnMouse = Marshal.GetFunctionPointerForDelegate( _onMouse );
            header->OnPlayerPositionChanged = Marshal.GetFunctionPointerForDelegate( _onUpdatePlayerPosition );
            header->OnClientClosing = Marshal.GetFunctionPointerForDelegate( _onClientClose );
            header->OnInitialize = Marshal.GetFunctionPointerForDelegate( _onInitialize );
            header->OnConnected = Marshal.GetFunctionPointerForDelegate( _onConnected );
            header->OnDisconnected = Marshal.GetFunctionPointerForDelegate( _onDisconnected );
            //header->OnFocusGained = Marshal.GetFunctionPointerForDelegate( _onFocusGained );
            //header->OnFocusLost = Marshal.GetFunctionPointerForDelegate( _onFocusLost );

            return true;
        }
        public unsafe override bool InstallHooks(IntPtr pluginPtr)
        {
          //  Engine.MainWindow.SafeAction( (s) => {
                Engine.MainWindow.MainForm_EndLoad();
          //  } );
            return true;
        }
        private void Tick()
        {
            Application.DoEvents();
        }
        
        private void OnPlayerPositionChanged(int x, int y, int z)
        {
            World.Player.Position = new Point3D(x, y, z);
        }

        private unsafe bool OnRecv(byte[] data, int length)
        {
            m_In += (uint)length;
            fixed (byte* ptr = data)
            {
                PacketReader p = new PacketReader(ptr, length, PacketsTable.IsDynLength(data[0]));
                Packet packet = new Packet(data, length, p.DynamicLength);

                return !PacketHandler.OnServerPacket(p.PacketID, p, packet);
            }
        }

        private unsafe bool OnSend(byte[] data, int length)
        {
            m_Out += (uint)length;
            fixed (byte* ptr = data)
            {
                PacketReader p = new PacketReader(ptr, length, PacketsTable.IsDynLength( data[0] ) ); ;
                Packet packet = new Packet(data, length, p.DynamicLength);

                return !PacketHandler.OnClientPacket(p.PacketID, p, packet);
            }
        }
        private void OnMouseHandler(int button, int wheel)
        {
            if (button > 4)
                button = 3;
            else if (button > 3)
                button = 2;
            else if (button > 2)
                button = 2;
            else if (button > 1)
                button = 1;

            HotKey.OnMouse(button, wheel);
        }
        private bool OnHotKeyHandler(int key, int mod, bool ispressed)
        {
            if (ispressed)
            {
                return HotKey.OnKeyDown((int)(key | mod));;
            }
            return true;
        }
        private void OnDisconnected()
        {
            PacketHandlers.Party.Clear();
            //TODO reset window title
            Engine.MainWindow.UpdateTitle();
            UOAssist.PostLogout();

            World.Player = null;
            World.Items.Clear();
            World.Mobiles.Clear();
            Macros.MacroManager.Stop();
            ActionQueue.Stop();
            Counter.Reset();
            GoldPerHourTimer.Stop();
            BandageTimer.Stop();
            GateTimer.Stop();
            BuffsTimer.Stop();
            StealthSteps.Unhide();
            Engine.MainWindow.OnLogout();
            if (Engine.MainWindow.MapWindow != null)
                Engine.MainWindow.MapWindow.Close();
            PacketHandlers.Party.Clear();
            PacketHandlers.IgnoreGumps.Clear();
            Config.Save();
        }
        private void OnConnected()
        {
            m_ConnectionStart = DateTime.UtcNow;
           
        }
        private void OnClientClosing()
        {
            var last = Console.BackgroundColor;
            var lastFore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Closing Razor instance");
            Console.BackgroundColor = last;
            Console.ForegroundColor = lastFore;
        }
        private void OnInitialize()
        {
            var last = Console.BackgroundColor;
            var lastFore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Green;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Initialized Razor instance");
            Console.BackgroundColor = last;
            Console.ForegroundColor = lastFore;
        }
        public override void SetConnectionInfo(IPAddress addr, int port)
        {

        }

        public override void SetNegotiate(bool negotiate)
        {

        }

        public override bool Attach(int pid)
        {
            return false;
        }

        public override void Close()
        {

        }

        public override void UpdateTitleBar()
        {
            if (!ClientRunning)
                return;

            if (World.Player != null && Config.GetBool("TitleBarDisplay"))
            {
                ResetTitleBarBuilder();

                TitleBarBuilder.Replace(@"{char}", World.Player.Name);

                TitleBarBuilder.Replace(@"{crimtime}", World.Player.CriminalTime != 0 ? $"{World.Player.CriminalTime}" : "-");

                TitleBarBuilder.Replace(@"{hp}", $"{World.Player.Hits}");
                TitleBarBuilder.Replace(@"{mana}", $"{World.Player.Mana}");
                TitleBarBuilder.Replace(@"{stam}", $"{World.Player.Stam}");

                TitleBarBuilder.Replace(@"{weight}", World.Player.Weight.ToString());

                TitleBarBuilder.Replace(@"{bandage}", BandageTimer.Running ? $"{BandageTimer.Count}" : "-");

                TitleBarBuilder.Replace(@"{statbar}", string.Empty);
                TitleBarBuilder.Replace(@"{mediumstatbar}", string.Empty);
                TitleBarBuilder.Replace(@"{largestatbar}", string.Empty);
                
                foreach (Counter c in Counter.List)
                {
                    if (c.Enabled)
                        TitleBarBuilder.Replace($"{{{c.Format}}}", c.GetTitlebarString(false, false));
                }

                base.UpdateTitleBar();
            }
            else
            {
                SetTitleStr(string.Empty);
            }
        }


        public override void SetTitleStr( string str )
        {
            _setTitle( str );
        }

        public override bool OnMessage(MainForm razor, uint wParam, int lParam)
        {
            return false;
        }

        public override bool OnCopyData(IntPtr wparam, IntPtr lparam)
        {
            return false;
        }

        public override void SendToServer(Packet p)
        {
            _sendToServer(p.Compile(), (int)p.Length);
        }

        public override void SendToServer(PacketReader pr)
        {
            SendToServer(MakePacketFrom(pr));
        }

        public override void SendToClient(Packet p)
        {
            _sendToClient(p.Compile(), (int)p.Length);

        }

        public override void ForceSendToClient(Packet p)
        {
            _sendToClient(p.Compile(), (int)p.Length);
        }

        public override void ForceSendToServer(Packet p)
        {
            _sendToServer(p.Compile(), (int)p.Length);
        }

        public override void SetPosition(uint x, uint y, uint z, byte dir)
        {

        }

        public override string GetClientVersion()
        {
            return m_ClientVersion;
        }

        public override IntPtr GetWindowHandle()
        {
            return m_ClientWindow;
        }

        public override uint TotalDataIn()
        {
            return m_In;
        }

        public override uint TotalDataOut()
        {
            return m_Out;
        }

        internal override void RequestMove( Direction m_Dir )
        {
            _requestMove( (int)m_Dir, true );
        }

        public void OnFocusGained()
        {
            if (Config.GetBool("AlwaysOnTop"))
            {
                if (!Engine.MainWindow.TopMost)
                {
                    Engine.MainWindow.TopMost = true;
                    Platform.SetForegroundWindow(GetWindowHandle());
                }
            }

            // always use smartness for the map window
            if (Engine.MainWindow.MapWindow != null && Engine.MainWindow.MapWindow.Visible)
            {
                if (!Engine.MainWindow.MapWindow.TopMost)
                {
                    Engine.MainWindow.MapWindow.TopMost = true;
                    Engine.MainWindow.MapWindow.BringToFront();
                }
            }
        }

        public void OnFocusLost()
        {
            if (Config.GetBool("AlwaysOnTop"))
            {
                if (Engine.MainWindow.TopMost)
                {
                    Engine.MainWindow.TopMost = false;
                    Engine.MainWindow.SendToBack();
                }
            }

            // always use smartness for the map window
            if (Engine.MainWindow.MapWindow != null && Engine.MainWindow.MapWindow.Visible)
            {
                if (Engine.MainWindow.MapWindow.TopMost)
                {
                    Engine.MainWindow.MapWindow.TopMost = false;
                    Engine.MainWindow.MapWindow.SendToBack();
                }
            }
        }
    }
}