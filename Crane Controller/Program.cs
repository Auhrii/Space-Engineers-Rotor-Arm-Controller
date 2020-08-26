using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {
        // SETTINGS //

        public const string version = "v1.0.0";
        public const string cabinName = "Docking Arm Cabin";
        public const string rotorName = "Docking Arm Rotor";
		public const string connectorName = "Docking Arm Connector";
        public const string pistonName = "Docking Arm Piston";
		public const string areaStatusPanelName = "Pad Status Panel";
		public const string callPanelName = "Pad Call Panel";

        // RUNTIME VARIABLES //

        private int? callOverride = null;
		private float rotorMultiplier = 0;
		private float lastRotorInput = 0;

		private IMyCockpit craneCabin;

		private IMyShipConnector craneConnector;
		private IMyTextSurface connectorMonitor;
		private StringBuilder connectorOutput = new StringBuilder();

		private IMyPistonBase cranePiston;
		private IMyTextSurface pistonMonitor;
		private StringBuilder pistonOutput = new StringBuilder();

		private IMyMotorAdvancedStator craneRotor;
		private IMyTextSurface rotorMonitor;
		private StringBuilder rotorOutput = new StringBuilder();

		private List<IMyTextPanel> areaStatusPanels = new List<IMyTextPanel>();
		private List<IMyButtonPanel> callPanels = new List<IMyButtonPanel>();

        // MAIN FUNCTIONS //

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

		public void UpdateCallPanels() {
			if (craneConnector == null || cranePiston == null || craneRotor == null) {
				return;
			}

			for (int key = 0; key < callPanels.Count; key++) {
				string areaPrefix = callPanels[key].CustomName.Substring(0, callPanels[key].CustomName.Length - callPanelName.Length);
				IMyShipConnector areaConnector = GridTerminalSystem.GetBlockWithName(areaPrefix + "Connector") as IMyShipConnector;
				IMyTextSurfaceProvider panelScreens = (IMyTextSurfaceProvider)callPanels[key];
				IMyTextSurface callScreen = panelScreens.GetSurface(0);

				if (areaConnector != null) {
					if (callOverride == null && areaConnector.Status == MyShipConnectorStatus.Connected && areaConnector.OtherConnector == craneConnector) {
						callScreen.FontColor = Color.Green;
					} else {
						callScreen.FontColor = callOverride != null ? Color.Yellow : Color.White;
					}
				} else {
					callScreen.FontColor = Color.Red;
				}
			}

			UpdateStatusPanels();
		}

		public void UpdateStatusPanels() {
			for (int key = 0; key < areaStatusPanels.Count; key++) {
				string areaPrefix = areaStatusPanels[key].CustomName.Substring(0, areaStatusPanels[key].CustomName.Length - areaStatusPanelName.Length);
				IMyShipConnector areaConnector = GridTerminalSystem.GetBlockWithName(areaPrefix + "Connector") as IMyShipConnector;
				StringBuilder panelOut = new StringBuilder();
				float totalHydrogen = 0;
				float totalOxygen = 0;

				panelOut.Append(areaPrefix.Substring(0,areaPrefix.Length - 1).ToUpper());
				panelOut.Append("\n\n");

				if (areaConnector != null && areaConnector.Status == MyShipConnectorStatus.Connected && areaConnector.OtherConnector == craneConnector) {
					panelOut.Append("ARM DOCKED\n\n");
				} else {
					panelOut.Append("ARM NOT DOCKED\n\n");
				}

				List<IMyGasTank> hydrogenTanks = new List<IMyGasTank>();
				GridTerminalSystem.GetBlocksOfType(hydrogenTanks, hydrogenTank => hydrogenTank.CustomName.Contains(areaPrefix + "Pad Hydrogen"));
				if (hydrogenTanks.Count > 0) {
					for (int hKey = 0; hKey < hydrogenTanks.Count; hKey++) {
						totalHydrogen += (float)hydrogenTanks[hKey].FilledRatio;
					}

					panelOut.Append(hydrogenTanks.Count.ToString());
					panelOut.Append("x H2 | ");
					panelOut.Append(Math.Floor((totalHydrogen/hydrogenTanks.Count) * 100).ToString("000.##"));
					panelOut.Append("%\n");
				} else {
					panelOut.Append("0x H2 | ERR!\n");
				}

				List<IMyGasTank> oxygenTanks = new List<IMyGasTank>();
				GridTerminalSystem.GetBlocksOfType(oxygenTanks, oxygenTank => oxygenTank.CustomName.Contains(areaPrefix + "Pad Oxygen"));
				if (oxygenTanks.Count > 0) {
					for (int oKey = 0; oKey < oxygenTanks.Count; oKey++) {
						totalOxygen += (float)oxygenTanks[oKey].FilledRatio;
					}

					panelOut.Append(oxygenTanks.Count.ToString());
					panelOut.Append("x O2 | ");
					panelOut.Append(Math.Floor((totalOxygen / oxygenTanks.Count) * 100).ToString("000.##"));
					panelOut.Append("%");
				} else {
					panelOut.Append("0x O2 | ERR!");
				}

				areaStatusPanels[key].WriteText(panelOut, false);
			}
		}

        public void Main(string argument, UpdateType updateSource) {
			// SETUP

			craneCabin = GridTerminalSystem.GetBlockWithName(cabinName) as IMyCockpit;
			craneRotor = GridTerminalSystem.GetBlockWithName(rotorName) as IMyMotorAdvancedStator;
			craneConnector = GridTerminalSystem.GetBlockWithName(connectorName) as IMyShipConnector;
			cranePiston = GridTerminalSystem.GetBlockWithName(pistonName) as IMyPistonBase;

			if (callPanels.Count == 0) {
				List<IMyButtonPanel> tempPanels = new List<IMyButtonPanel>();
				GridTerminalSystem.GetBlocksOfType(tempPanels, tempPanel => tempPanel.CustomName.Contains(callPanelName));

				for (int key = 0; key < tempPanels.Count; key++) {
					IMyTextSurfaceProvider panelLCD = (IMyTextSurfaceProvider)tempPanels[key];

					if (panelLCD.GetSurface(0) != null) {
						callPanels.Add(tempPanels[key]);
					}
				}
			}

			if (areaStatusPanels.Count == 0) {
				GridTerminalSystem.GetBlocksOfType(areaStatusPanels, tempPanel => tempPanel.CustomName.Contains(areaStatusPanelName));
			}

			if (craneCabin != null) {
				pistonMonitor = craneCabin.GetSurface(1);
				connectorMonitor = craneCabin.GetSurface(2);
				rotorMonitor = craneCabin.GetSurface(3);
			}

			// END SETUP

			if (argument != "" && callOverride == null) {
				if (craneConnector == null || cranePiston == null || craneRotor == null) {
					return;
				}

				callOverride = Math.Min(Math.Max((int.Parse(argument) * 45) - 180, -135), 135);
				UpdateCallPanels();
				lastRotorInput = 0;
				rotorMultiplier = 0;
				craneConnector.Disconnect();

				if (Math.Floor((craneRotor.Angle * (180 / Math.PI)) + 0.5) < callOverride) {
					craneRotor.UpperLimitDeg = (float)callOverride;
					craneRotor.TargetVelocityRPM = 1;
				} else {
					craneRotor.LowerLimitDeg = (float)callOverride;
					craneRotor.TargetVelocityRPM = -1;
				}
			}

			if ((updateSource & UpdateType.Update100) != 0) {
				Runtime.UpdateFrequency = craneCabin.IsUnderControl || rotorMultiplier != 0 || callOverride != null ? UpdateFrequency.Update1 | UpdateFrequency.Update100 : UpdateFrequency.Update100;
				UpdateCallPanels();
			}

			if (callOverride == null) {
				if ((updateSource & UpdateType.Update1) != 0 && craneCabin != null) {
					pistonOutput.Length = 0;
					connectorOutput.Length = 0;
					rotorOutput.Length = 0;

					connectorOutput.Append("\n\n\n\n");

					if (craneCabin.IsUnderControl) {
						pistonOutput.Append("\n\n\n\nPISTON\n");
						rotorOutput.Append("\n\n\nROTOR\n");

						if (craneConnector != null) {
							if (craneCabin.MoveIndicator.Z > 0) {
								craneConnector.Disconnect();
							}

							if (craneConnector.Status == MyShipConnectorStatus.Unconnected) {
								connectorOutput.Append("DISCONNECTED");
								connectorMonitor.FontColor = Color.Red;
							} else if (craneConnector.Status == MyShipConnectorStatus.Connected) {
								connectorOutput.Append("CONNECTED\n");
								connectorMonitor.FontColor = Color.Green;
								string otherName = craneConnector.OtherConnector.CustomName;
								connectorOutput.Append(otherName.Substring(0, otherName.Length - 10));
							} else {
								connectorOutput.Append("READY");
								connectorMonitor.FontColor = Color.Yellow;
							}
						}

						if (cranePiston != null) {
							cranePiston.Velocity = (float)ClampVelocity(-craneCabin.MoveIndicator.Z, 1);

							pistonOutput.Append("[ ");
							pistonOutput.Append((Math.Floor(cranePiston.CurrentPosition * 10) / 10).ToString("F1"));
							pistonOutput.Append("m ]");
						}

						if (craneRotor != null) {
							if (craneConnector.Status != MyShipConnectorStatus.Connected) {
								if (craneCabin.MoveIndicator.X != 0 && Math.Sign(craneCabin.MoveIndicator.X) != Math.Sign(lastRotorInput)) {
									rotorMultiplier *= -1;
								}

								lastRotorInput = (craneCabin.MoveIndicator.X == 0 ? lastRotorInput : craneCabin.MoveIndicator.X);
								if (craneCabin.MoveIndicator.X != 0) {
									rotorMultiplier = Math.Min(Math.Max(rotorMultiplier + (Math.Abs(craneCabin.MoveIndicator.X) * 0.02f), -1), 1);
								} else {
									rotorMultiplier += Math.Max(Math.Min(Math.Abs(0 - rotorMultiplier), 0.02f), 0) * -Math.Sign(rotorMultiplier);
								}

								craneRotor.TargetVelocityRPM = (float)ClampVelocity(lastRotorInput, 1) * rotorMultiplier;
							}

							rotorOutput.Append("[ ");
							rotorOutput.Append(Math.Floor((craneRotor.Angle * (180 / Math.PI)) + 0.5).ToString("000.##"));
							rotorOutput.Append("° ]");
						}
					} else {
						connectorOutput.Append("STANDING BY");
						connectorMonitor.FontColor = Color.White;

						if (rotorMultiplier > 0) {
							rotorMultiplier = Math.Max(rotorMultiplier - 0.02f, 0);
						} else if (rotorMultiplier < 0) {
							rotorMultiplier = Math.Min(rotorMultiplier + 0.02f, 0);
						} else {
							lastRotorInput = 0;
						}

						craneRotor.TargetVelocityRPM = (float)ClampVelocity(lastRotorInput, 1) * rotorMultiplier;
					}

					pistonMonitor.WriteText(pistonOutput, false);
					connectorMonitor.WriteText(connectorOutput, false);
					rotorMonitor.WriteText(rotorOutput, false);
                } else if (craneCabin == null) {
					lastRotorInput = 0;
					rotorMultiplier = 0;

					if (cranePiston != null) {
						cranePiston.Velocity = 0;
					}

					if (craneRotor != null) {
						craneRotor.TargetVelocityRPM = 0;
					}
				}
			} else {
				if (craneConnector == null || cranePiston == null || craneRotor == null) {
					callOverride = null;
					UpdateCallPanels();
					return;
				}

				if (Math.Abs((craneRotor.Angle * (180 / Math.PI)) - (float)callOverride) < 0.25) {
					craneRotor.TargetVelocityRPM = 0;
					craneRotor.UpperLimitDeg = 135;
					craneRotor.LowerLimitDeg = -135;

					cranePiston.Velocity = 1;

					if (Math.Abs(cranePiston.CurrentPosition - cranePiston.MaxLimit) < 0.1 && craneConnector.Status == MyShipConnectorStatus.Connectable) {
						cranePiston.Velocity = 0;
						craneConnector.Connect();
						callOverride = null;
						UpdateCallPanels();
						return;
					}
				} else {
					cranePiston.Velocity = -1;
				}

				if (craneCabin != null) {
					connectorOutput.Length = 0;
					rotorOutput.Length = 0;
					pistonOutput.Length = 0;

					connectorOutput.Append("\n\n\n\nAUTO-DOCKING\n");
					connectorOutput.Append(((float)callOverride).ToString("000.##"));
					connectorOutput.Append("°");
					connectorMonitor.FontColor = Color.Blue;

					pistonOutput.Append("\n\n\n\nPISTON\n");
					pistonOutput.Append("[ ");
					pistonOutput.Append((Math.Floor(cranePiston.CurrentPosition * 10) / 10).ToString("F1"));
					pistonOutput.Append("m ]");

					rotorOutput.Append("\n\n\nROTOR\n");
					rotorOutput.Append("[ ");
					rotorOutput.Append(Math.Floor((craneRotor.Angle * (180 / Math.PI)) + 0.5).ToString("000.##"));
					rotorOutput.Append("° ]");

					pistonMonitor.WriteText(pistonOutput, false);
					connectorMonitor.WriteText(connectorOutput, false);
					rotorMonitor.WriteText(rotorOutput, false);
				}
			}
		}

        private double ClampVelocity(double input, double max) {
            input = Math.Max(Math.Min(input, max), -max);

            return input;
        }
    }
}

