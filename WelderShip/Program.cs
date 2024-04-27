using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
using PID_Controller;
using VRage.Utils;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const UpdateType updateFreq = UpdateType.Update1;
        const int framesPerUpdate = 1;

        PIDController HingesPID;
        PIDController PitchPID;
        PIDController RollPID;
        PIDController YawPID;

        IMyMotorAdvancedStator HingePitch;
        IMyMotorAdvancedStator HingeYaw;

        IMyShipController Cockpit;

        IMyGyro Gyro;

        bool ShipControls;

        Vector3D YawSetpoint;

        float YawSensitivity;

        public Program()
        {
            string[] blockMappings = Me.CustomData.Split('\n');

            foreach (string mapping in blockMappings)
            {
                string[] map = mapping.Split(':');

                if (map.Length != 2)
                {
                    Echo($"Mapping \"{mapping}\" is not valid.");
                    continue;
                }

                switch (map[0])
                {
                    case nameof(YawSensitivity):
                        {
                            if (!float.TryParse(map[1], out YawSensitivity))
                            {
                                Echo($"Could not parse value \"{map[1]}\" for \"{map[0]}\"");
                            }
                            continue;
                        }
                    case nameof(HingesPID):
                        {
                            string[] pidVals = map[1].Split(',');

                            if (pidVals.Length != 3)
                            {
                                Echo("PID values should follow the format <Kp>,<Ki>,<Kd>");
                            }
                            else
                            {
                                float Kp, Ki, Kd;
                                if (!(float.TryParse(pidVals[0], out Kp) && float.TryParse(pidVals[1], out Ki) && float.TryParse(pidVals[2], out Kd)))
                                {
                                    Echo("Could not parse PID values to floats");
                                }
                                else
                                {
                                    HingesPID = new PIDController(Kp, Ki, Kd);
                                }
                            }

                            continue;
                        }
                    case nameof(PitchPID):
                        {
                            string[] pidVals = map[1].Split(',');

                            if (pidVals.Length != 3)
                            {
                                Echo("PID values should follow the format <Kp>,<Ki>,<Kd>");
                            }
                            else
                            {
                                float Kp, Ki, Kd;
                                if (!(float.TryParse(pidVals[0], out Kp) && float.TryParse(pidVals[1], out Ki) && float.TryParse(pidVals[2], out Kd)))
                                {
                                    Echo("Could not parse PID values to floats");
                                }
                                else
                                {
                                    PitchPID = new PIDController(Kp, Ki, Kd);
                                }
                            }

                            continue;
                        }
                    case nameof(RollPID):
                        {
                            string[] pidVals = map[1].Split(',');

                            if (pidVals.Length != 3)
                            {
                                Echo("PID values should follow the format <Kp>,<Ki>,<Kd>");
                            }
                            else
                            {
                                float Kp, Ki, Kd;
                                if (!(float.TryParse(pidVals[0], out Kp) && float.TryParse(pidVals[1], out Ki) && float.TryParse(pidVals[2], out Kd)))
                                {
                                    Echo("Could not parse PID values to floats");
                                }
                                else
                                {
                                    RollPID = new PIDController(Kp, Ki, Kd);
                                }
                            }

                            continue;
                        }
                    case nameof(YawPID):
                        {
                            string[] pidVals = map[1].Split(',');

                            if (pidVals.Length != 3)
                            {
                                Echo("PID values should follow the format <Kp>,<Ki>,<Kd>");
                            }
                            else
                            {
                                float Kp, Ki, Kd;
                                if (!(float.TryParse(pidVals[0], out Kp) && float.TryParse(pidVals[1], out Ki) && float.TryParse(pidVals[2], out Kd)))
                                {
                                    Echo("Could not parse PID values to floats");
                                }
                                else
                                {
                                    YawPID = new PIDController(Kp, Ki, Kd);
                                }
                            }

                            continue;
                        }
                }

                IMyTerminalBlock block = GridTerminalSystem.GetBlockWithName(map[1]);

                if (block == null)
                {
                    Echo($"No blocks found with name \"{map[1]}\".");
                    continue;
                }

                switch (map[0])
                {
                    case nameof(HingePitch):
                        {
                            HingePitch = block as IMyMotorAdvancedStator;
                            break;
                        }
                    case nameof(HingeYaw):
                        {
                            HingeYaw = block as IMyMotorAdvancedStator;
                            break;
                        }
                    case nameof(Cockpit):
                        {
                            Cockpit = block as IMyShipController;
                            break;
                        }
                    case nameof(Gyro):
                        {
                            Gyro = block as IMyGyro;
                            break;
                        }
                    default:
                        {
                            Echo($"Key \"{map[0]}\" is not understood by this script.");
                            break;
                        }
                }
            }

            YawSetpoint = Cockpit.WorldMatrix.Forward;

            YawSensitivity = 1f;

            Runtime.UpdateFrequency = (UpdateFrequency)((int)updateFreq / 32);

            ShipControls = true;

            Gyro.GyroOverride = true;
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            //if (!string.IsNullOrEmpty(argument))
            //{
            //    double val;
            //    if (double.TryParse(argument, out val))
            //    {
            //        HingeSetPoint = val * 3.14159 / 180f;
            //    }
            //}

            if (!string.IsNullOrEmpty(argument) && argument.Equals("toggle controls"))
            {
                ShipControls = !ShipControls;

                Echo("Toggling controls");
            }

            //if ((updateSource & UpdateType.Update10) != 0)
            //{
            //    Hinge.TargetVelocityRad = (float)HingesPID.Update(HingeSetPoint - Hinge.Angle, Hinge.Angle, 10f/60f);
            //}

            if ((updateSource & updateFreq) != 0)
            {
                if (!ShipControls)
                {
                    HingePitch.TargetVelocityRad = Cockpit.RotationIndicator.X * -0.1f;
                    HingeYaw.TargetVelocityRad = Cockpit.RotationIndicator.Y * 0.1f;

                    Gyro.Pitch = 0;
                }
                else
                {
                    HingePitch.TargetVelocityRad = (float)HingesPID.Update(- HingePitch.Angle, HingePitch.Angle, framesPerUpdate / 60f);
                    HingeYaw.TargetVelocityRad = (float)HingesPID.Update(-HingeYaw.Angle, HingeYaw.Angle, framesPerUpdate / 60f);

                    //YawSetpoint = YawSetpoint.Rotate(Cockpit.WorldMatrix.Up, framesPerUpdate / 60f * Cockpit.RotationIndicator.Y * YawSensitivity);
                    Gyro.Pitch = Cockpit.RotationIndicator.Y * YawSensitivity;
                }

                //Gyro.Pitch = (float)YawPID.Update((Cockpit.WorldMatrix.Forward - YawSetpoint).X, 0, framesPerUpdate / 60f);

                Vector3D localGravity = Vector3D.TransformNormal(Cockpit.GetTotalGravity(), MatrixD.Transpose(Cockpit.WorldMatrix));

                Gyro.Roll = (float)PitchPID.Update(-localGravity.Z, localGravity.Z, framesPerUpdate / 60f);
                Gyro.Yaw = (float)RollPID.Update(-localGravity.X, localGravity.X, framesPerUpdate / 60f);

                Echo($"{localGravity.X},{localGravity.Y},{localGravity.Z}");
            }
        }
    }
}
 