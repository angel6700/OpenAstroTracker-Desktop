// --------------------------------------------------------------------------------
//
// ASCOM Focuser driver for OpenAstroTracker
//
// Description:	This driver exposes the focuser API from OAT for OATs that have 
//				a focuser attached. The driver uses the standard LX200 protocol that 
//				OAT uses.
//
// Implements:	ASCOM Focuser interface version 3
// Author:		(LSK) Lutz Kretzschmar (lutz@stmuc.com)
//
// Edit Log:
//
// Date			Who	Version	Description
// -----------	---	-------	-------------------------------------------------------
// 02-May-2021	LSK	6.6.0.0	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//

using ASCOM.Astrometry.AstroUtils;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ASCOM.OpenAstroTracker.SharedResources;

namespace ASCOM.OpenAstroTracker
{
	//
	// DeviceID is ASCOM.OpenAstroTracker.Focuser
	//
	// The Guid attribute sets the CLSID for ASCOM.OpenAstroTracker.Focuser
	// The ClassInterface/None attribute prevents an empty interface called
	// _OpenAstroTracker from being created and used as the [default] interface
	//

	/// <summary>
	/// ASCOM Focuser Driver for OpenAstroTracker.
	/// </summary>
	[Guid("9a679a0d-cae3-473f-827d-94b359aaac7e")]
	[ProgId("ASCOM.OpenAstroTracker.Focuser")]
	[ServedClassName("OpenAstroTracker")]
	[ClassInterface(ClassInterfaceType.None)]
	public class Focuser : ReferenceCountedObjectBase, IFocuserV3
	{
		private string Version => GetType().Assembly.GetName().Version.ToString();

		/// <summary>
		/// ASCOM DeviceID (COM ProgID) for this driver.
		/// The DeviceID is used by ASCOM applications to load the driver at runtime.
		/// </summary>
		internal string _driverId = "ASCOM.OpenAstroTracker.Focuser";
		
		/// <summary>
		/// Logging callback for focuser
		/// </summary>
		private Action<SharedResources.LoggingFlags, string> logMessage;

		/// <summary>
		/// Driver description that displays in the ASCOM Chooser.
		/// </summary>
		private static string _driverDescription = "OpenAstroTracker Focuser";

		private ProfileData Profile => SharedResources.ReadProfile();

		/// <summary>
		/// Private variable to hold an ASCOM Utilities object
		/// </summary>
		private Util utilities;

		/// <summary>
		/// Private variable to hold an ASCOM AstroUtilities object to provide the Range method
		/// </summary>
		private AstroUtils astroUtilities;

		/// <summary>
		/// Variable to whether we are connected
		/// </summary>
		private bool _isConnected;

		/// <summary>
		/// Initializes a new instance of the <see cref="OpenAstroTracker"/> class.
		/// Must be public for COM registration.
		/// </summary>
		public Focuser()
		{
			_driverId = Marshal.GenerateProgIdForType(this.GetType());

			logMessage = SharedResources.LogMessageCallback;

			logMessage(LoggingFlags.Focuser, "Starting initialisation");

			utilities = new Util(); //Initialise util object
			astroUtilities = new AstroUtils(); // Initialise astro-utilities object

			logMessage(LoggingFlags.Focuser, "Completed initialisation");
		}


		//
		// PUBLIC COM INTERFACE IFocuserV3 IMPLEMENTATION
		//

		#region Common properties and methods.

		/// <summary>
		/// Displays the Setup Dialog form.
		/// If the user clicks the OK button to dismiss the form, then
		/// the new settings are saved, otherwise the old values are reloaded.
		/// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
		/// </summary>
		public void SetupDialog()
		{
			// consider only showing the setup dialog if not connected
			// or call a different dialog if connected
			if (IsConnected)
			{
				MessageBox.Show("OAT is connected, use Telescope driver to control focuser manually.");
			}

			using (var f = new SetupDialogForm(Profile, null, null))
			{
				if (f.ShowDialog() == DialogResult.OK)
				{
					SharedResources.WriteProfile(f.GetProfileData()); // Persist device configuration values to the ASCOM Profile store
				}
			}
		}

		public ArrayList SupportedActions
		{
			get
			{
				logMessage(LoggingFlags.Focuser, "SupportedActions Get => empty arraylist");
				return new ArrayList();
			}
		}

		public string Action(string actionName, string actionParameters)
		{
			logMessage(LoggingFlags.Focuser, "Action() - Not implemented");
			throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
		}

		public void CommandBlind(string command, bool raw)
		{
			SharedResources.OATCommandMutex.WaitOne();

			try
			{
				SharedResources.SendMessage(command);
			}
			catch (Exception ex)
			{
				logMessage(LoggingFlags.Focuser, $"CommandBlind - Exception{ex.Message}");
			}
			finally
			{
				SharedResources.OATCommandMutex.ReleaseMutex();
			}
		}

		public bool CommandBool(string command, bool raw)
		{
			throw new ASCOM.MethodNotImplementedException("CommandBool");
		}

		public string CommandString(string command, bool raw)
		{
			SharedResources.OATCommandMutex.WaitOne();

			try
			{
				var response = SharedResources.SendMessage(command);
				return response;
			}
			catch (Exception ex)
			{
				logMessage(LoggingFlags.Focuser, $"CommandString({command}) => Exception {ex.Message}");
				return "255";
			}
			finally
			{
				SharedResources.OATCommandMutex.ReleaseMutex();
			}
		}

		public bool Connected
		{
			get => IsConnected;
			set
			{
				if (IsConnected != value)
				{
					_isConnected = value;
					SharedResources.Connected = value;
				}
			}
		}

		public string Description
		{
			get
			{
				logMessage(LoggingFlags.Focuser, $"Description Get => {_driverDescription}");
				return _driverDescription;
			}
		}

		public string DriverInfo
		{
			get
			{
				Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
				string driverInfo = "OpenAstroTracker ASCOM driver version: " + Version;
				logMessage(LoggingFlags.Focuser, $"DriverInfo Get => {driverInfo}");
				return driverInfo;
			}
		}

		public string DriverVersion
		{
			get
			{
				logMessage(LoggingFlags.Focuser, $"DriverVersion Get => {Version}");
				return Version;
			}
		}

		public short InterfaceVersion
		{
			// set by the driver wizard
			get
			{
				logMessage(LoggingFlags.Focuser, "InterfaceVersion Get => 3");
				return 3;
			}
		}

		public string Name
		{
			get
			{
				string name = "OAT Focuser ASCOM";
				logMessage(LoggingFlags.Focuser, $"Name Get => {name}");
				return name;
			}
		}

		public void Dispose()
		{
			// Clean up the trace logger and util objects
			Connected = false;
						
			logMessage = null;
			utilities.Dispose();
			utilities = null;
			astroUtilities.Dispose();
			astroUtilities = null;
		}



		#endregion

		#region IFocuser Implementation

		private const int focuserMaxRange = 100000;
		private const int focuserSteps = 4000;

		public bool Absolute
		{
			get
			{
				logMessage(LoggingFlags.Focuser, "Absolute Get => true");
				return true; // This is an absolute focuser
			}
		}

		public void Halt()
		{
			logMessage(LoggingFlags.Focuser, "Halt - stopping focuser");
			CommandBlind(":FQ#", false);
			logMessage(LoggingFlags.Focuser, "Halt - complete");
		}

		public bool IsMoving
		{
			get
			{
				logMessage(LoggingFlags.Focuser, $"IsMoving Get");
				var response = CommandString(":FB#,n", false);
				bool moving = response == "1";
				logMessage(LoggingFlags.Focuser, $"IsMoving Get => {moving}");
				return moving;
			}
		}

		public bool Link
		{
			get
			{
				logMessage(LoggingFlags.Focuser, $"Link Get => {this.Connected}");
				return this.Connected; // Direct function to the connected method, the Link method is just here for backwards compatibility
			}
			set
			{
				logMessage(LoggingFlags.Focuser, $"Link Set - {value}");
				this.Connected = value; // Direct function to the connected method, the Link method is just here for backwards compatibility
			}
		}

		public int MaxIncrement
		{
			get
			{
				logMessage(LoggingFlags.Focuser, $"MaxIncrement Get => {focuserSteps}");
				return focuserSteps; // Maximum change in one move
			}
		}

		public int MaxStep
		{
			get
			{
				logMessage(LoggingFlags.Focuser, $"MaxStep Get => {focuserMaxRange}");
				return focuserMaxRange; // Maximum extent of the focuser, so position range is 0 to this number
			}
		}

		public void Move(int newPosition)
		{
			logMessage(LoggingFlags.Focuser, $"MoveAbs To({newPosition})");
			int currentPosition = Convert.ToInt32(CommandString($":Fp#,#", false));
			int moveBy = newPosition - currentPosition;
			CommandBlind($":FM{moveBy}#", false);
			logMessage(LoggingFlags.Focuser, $"MoveAbs To({newPosition}) - complete");
		}

		public int Position
		{
			get
			{
				logMessage(LoggingFlags.Focuser, $"Position Get");
				var response = CommandString(":Fp#,#", false);
				logMessage(LoggingFlags.Focuser, $"Position Get => {response}");
				return Convert.ToInt32(response); // Return the focuser position
			}
		}

		public double StepSize
		{
			get
			{
				// We don't know the microns for each step
				logMessage(LoggingFlags.Focuser, "StepSize Get - Not implemented");
				throw new ASCOM.PropertyNotImplementedException("StepSize", false);
			}
		}

		public bool TempComp
		{
			get
			{
				logMessage(LoggingFlags.Focuser, "TempComp Get => false");
				return false;
			}
			set
			{
				logMessage(LoggingFlags.Focuser, "TempComp Set - Not implemented");
				throw new ASCOM.PropertyNotImplementedException("TempComp", false);
			}
		}

		public bool TempCompAvailable
		{
			get
			{
				logMessage(LoggingFlags.Focuser, "TempCompAvailable Get => false");
				return false; // Temperature compensation is not available in this driver
			}
		}

		public double Temperature
		{
			get
			{
				logMessage(LoggingFlags.Focuser, "Temperature Get - Not implemented");
				throw new ASCOM.PropertyNotImplementedException("Temperature", false);
			}
		}

		#endregion

		#region Private properties and methods

#if INPROCESS

		#region ASCOM Registration

		// Register or unregister driver for ASCOM. This is harmless if already
		// registered or unregistered. 
		//
		/// <summary>
		/// Register or unregister the driver with the ASCOM Platform.
		/// This is harmless if the driver is already registered/unregistered.
		/// </summary>
		/// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
		private static void RegUnregASCOM(bool bRegister)
		{
			using (var P = new ASCOM.Utilities.Profile())
			{
				P.DeviceType = "Focuser";
				if (bRegister)
				{
					P.Register(_driverId, _driverDescription);
				}
				else
				{
					P.Unregister(_driverId);
				}
			}
		}

		/// <summary>
		/// This function registers the driver with the ASCOM Chooser and
		/// is called automatically whenever this class is registered for COM Interop.
		/// </summary>
		/// <param name="t">Type of the class being registered, not used.</param>
		/// <remarks>
		/// This method typically runs in two distinct situations:
		/// <list type="numbered">
		/// <item>
		/// In Visual Studio, when the project is successfully built.
		/// For this to work correctly, the option <c>Register for COM Interop</c>
		/// must be enabled in the project settings.
		/// </item>
		/// <item>During setup, when the installer registers the assembly for COM Interop.</item>
		/// </list>
		/// This technique should mean that it is never necessary to manually register a driver with ASCOM.
		/// </remarks>
		[ComRegisterFunction]
		public static void RegisterASCOM(Type t)
		{
			RegUnregASCOM(true);
		}

		/// <summary>
		/// This function unregisters the driver from the ASCOM Chooser and
		/// is called automatically whenever this class is unregistered from COM Interop.
		/// </summary>
		/// <param name="t">Type of the class being registered, not used.</param>
		/// <remarks>
		/// This method typically runs in two distinct situations:
		/// <list type="numbered">
		/// <item>
		/// In Visual Studio, when the project is cleaned or prior to rebuilding.
		/// For this to work correctly, the option <c>Register for COM Interop</c>
		/// must be enabled in the project settings.
		/// </item>
		/// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
		/// </list>
		/// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
		/// </remarks>
		[ComUnregisterFunction]
		public static void UnregisterASCOM(Type t)
		{
			RegUnregASCOM(false);
		}

		#endregion
#endif

		/// <summary>
		/// Returns true if there is a valid connection to the driver hardware
		/// </summary>
		private bool IsConnected => SharedResources.Connected && _isConnected;

#endregion
	}
}
