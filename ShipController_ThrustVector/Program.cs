using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using SpaceEngineers.Game.Utils;
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
using EmptyKeys.UserInterface.Generated.DataTemplatesContracts_Bindings;
using VRage.Utils;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const UpdateType updateFreq = UpdateType.Update1;
        const int framesPerUpdate = 1;

        IMyShipController Cockpit;

        List<IMyLandingGear> LandingGears = new List<IMyLandingGear>();

        IMyGyro Gyro;

        List<IMyMotorAdvancedStator> LeftRotors;
        List<IMyMotorAdvancedStator> RightRotors;


        List<IMyThrust> MainThrusters;
        List<IMyThrust> RightThrusters;
        List<IMyThrust> LeftThrusters;

        PIDController RotorController;
        PIDController RollController;

        float PitchSensitivity;
        float YawSensitivity;
        float InputSensitivity;
        float DampingSensitivity;

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
                    case nameof(PitchSensitivity):
                        {
                            if (!float.TryParse(map[1], out PitchSensitivity))
                            {
                                Echo($"Could not parse value \"{map[1]}\" for \"{map[0]}\"");
                            }
                            continue;
                        }
                    case nameof(YawSensitivity):
                        {
                            if (!float.TryParse(map[1], out YawSensitivity))
                            {
                                Echo($"Could not parse value \"{map[1]}\" for \"{map[0]}\"");
                            }
                            continue;
                        }
                    case nameof(InputSensitivity):
                        {
                            if (!float.TryParse(map[1], out InputSensitivity))
                            {
                                Echo($"Could not parse value \"{map[1]}\" for \"{map[0]}\"");
                            }
                            continue;
                        }
                    case nameof(DampingSensitivity):
                        {
                            if (!float.TryParse(map[1], out DampingSensitivity))
                            {
                                Echo($"Could not parse value \"{map[1]}\" for \"{map[0]}\"");
                            }
                            continue;
                        }                        
                    case nameof(RotorController):
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
                                    RotorController = new PIDController(Kp, Ki, Kd);
                                }
                            }

                            continue;
                        }
                    case nameof(RollController):
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
                                    RollController = new PIDController(Kp, Ki, Kd);
                                }
                            }

                            continue;
                        }
                }

                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

                GridTerminalSystem.SearchBlocksOfName(map[1], blocks);

                if (blocks.Count == 0)
                {
                    Echo($"No blocks found with name \"{map[1]}\".");
                    continue;
                }

                switch (map[0])
                {
                    case nameof(LeftRotors):
                        {
                            LeftRotors = blocks.ConvertAll(block => block as IMyMotorAdvancedStator);
                            break;
                        }
                    case nameof(RightRotors):
                        {
                            RightRotors = blocks.ConvertAll(block => block as IMyMotorAdvancedStator);
                            break;
                        }
                    case nameof(MainThrusters):
                        {
                            MainThrusters = blocks.ConvertAll(block => block as IMyThrust);
                            break;
                        }
                    case nameof(RightThrusters):
                        {
                            RightThrusters = blocks.ConvertAll(block => block as IMyThrust);
                            break;
                        }
                    case nameof(LeftThrusters):
                        {
                            LeftThrusters = blocks.ConvertAll(block => block as IMyThrust);
                            break;
                        }
                    case nameof(LandingGears):
                        {
                            LandingGears = blocks.ConvertAll(block => block as IMyLandingGear);
                            break;
                        }
                    case nameof(Gyro):
                        {
                            Gyro = blocks.ConvertAll(block => block as IMyGyro).FirstOrDefault(gyro => gyro != null);
                            break;
                        }
                    case nameof(Cockpit):
                        {
                            Cockpit = blocks.ConvertAll(block => block as IMyShipController).FirstOrDefault(ship => ship != null);
                            break;
                        }

                }
            }

            Runtime.UpdateFrequency = (UpdateFrequency)((int)updateFreq / 32);

            Gyro.GyroOverride = true;

            Cockpit.ControlThrusters = false;
        }

        public void Save()
        {
            
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & updateFreq) != 0)
            {
                if (LandingGears.Any(lg => lg.IsLocked))
                {
                    MainThrusters.ForEach(thruster => thruster.ThrustOverride = 0);
                    LeftThrusters.ForEach(thruster => thruster.ThrustOverride = 0);
                    RightThrusters.ForEach(thruster => thruster.ThrustOverride = 0);

                    return;
                }

                Vector2 rotationInput = Cockpit.RotationIndicator;

                Gyro.Roll = rotationInput.X * PitchSensitivity;
                Gyro.Yaw = rotationInput.Y * YawSensitivity;

                Vector3D localGravity = Vector3D.TransformNormal(Cockpit.GetTotalGravity(), MatrixD.Transpose(Cockpit.WorldMatrix));

                double localGravityRatio;
                if (localGravity.Y == 0)
                {
                    localGravityRatio = 0;
                }
                else
                {
                    localGravityRatio = localGravity.X / localGravity.Y;
                }

                Gyro.Pitch = (float)RollController.Update(-Math.Atan(localGravityRatio), 0, framesPerUpdate / 10f);

                Vector3D localVelocity = Vector3D.TransformNormal(Cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(Cockpit.WorldMatrix));

                float mass = Cockpit.CalculateShipMass().PhysicalMass;
                float maxThrust = MainThrusters.Sum(thruster => thruster.MaxThrust);

                double strafeInput = Cockpit.MoveIndicator.X;

                if (strafeInput < -0.1)
                {
                    RightThrusters.ForEach(thruster => thruster.ThrustOverridePercentage = 1);
                    LeftThrusters.ForEach(thruster => thruster.ThrustOverridePercentage = 0);
                }
                else if (strafeInput > 0.1)
                {
                    RightThrusters.ForEach(thruster => thruster.ThrustOverridePercentage = 0);
                    LeftThrusters.ForEach(thruster => thruster.ThrustOverridePercentage = 1);
                }

                Echo("");

                Vector2D reverseGrav = new Vector2D(localGravity.Z * mass, -localGravity.Y * mass); // (forward, up)
                Vector2D input = new Vector2D(-Cockpit.MoveIndicator.Z, Cockpit.MoveIndicator.Y); // (forward, up)

                input *= InputSensitivity * mass;

                if (double.IsNaN(input.X))
                {
                    input.X = 0;
                }
                if (double.IsNaN(input.Y))
                {
                    input.Y = 0;
                }

                double negGravity = reverseGrav.Length();

                if (negGravity > maxThrust)
                {
                    negGravity = maxThrust;
                }

                double alpha = Math.Atan2(input.Y, input.X);
                double gamma = negGravity > 0 ? Math.Acos(reverseGrav.Y / negGravity) : 0;
                double sinBeta = Math.Sin(180 - alpha - gamma);

                double maxInputThrust = sinBeta > 0 ? maxThrust * Math.Sin(alpha + gamma - Math.Asin(negGravity * sinBeta / maxThrust)) / sinBeta : maxThrust;

                if (Cockpit.DampenersOverride)
                {
                    Vector2D reverseVel = new Vector2D(localVelocity.Z, -localVelocity.Y) * mass * DampingSensitivity;

                    if (input.X != 0)
                    {
                        reverseVel.X = 0;
                    }

                    if (input.Y != 0)
                    {
                        reverseVel.Y = 0;
                    }

                    if (reverseVel.Length() > maxInputThrust)
                    {
                        reverseVel.Normalize();
                        reverseVel *= maxInputThrust;
                    }

                    input += reverseVel;
                }

                if (input.Length() > maxInputThrust && input.Length() != 0)
                {
                    input.Normalize();
                    input *= maxInputThrust;
                }

                Vector2D thrustVec = input + reverseGrav;

                //if (localVelocity.Length() > 100 - 0.5) //100 m/s
                //{
                //    Vector2D planarVel = new Vector2D(-localVelocity.Z, localVelocity.Y);

                //    double projection = Vector2D.Dot(planarVel, thrustVec) / planarVel.Length();

                //    if (projection > 0)
                //    {
                //        thrustVec -= planarVel * projection / planarVel.Length();
                //    }
                //}

                double rotorAngle = Math.Atan2(thrustVec.X, thrustVec.Y);

                RightRotors.ForEach(rotor =>
                {
                    rotor.TargetVelocityRad = -(float)RotorController.Update((rotorAngle - (rotor.Angle + Math.PI / 2) + 3 * Math.PI) % (2 * Math.PI) - Math.PI, 0, framesPerUpdate / 10f);
                });

                LeftRotors.ForEach(rotor =>
                {
                    rotor.TargetVelocityRad = (float)RotorController.Update((rotorAngle - (Math.PI / 2 - rotor.Angle) + 3 * Math.PI) % (2 * Math.PI) - Math.PI, 0, framesPerUpdate / 10f);
                });

                double avgRotorAngle = (RightRotors.Average(rotor => (rotor.Angle + Math.PI / 2 + 3 * Math.PI) % (2 * Math.PI) - Math.PI) + LeftRotors.Average(rotor => (Math.PI / 2 - rotor.Angle + 3 * Math.PI) % (2 * Math.PI) - Math.PI)) / 2d;

                double angleBetweenRotorAndThrustVec = avgRotorAngle - rotorAngle;

                if (Math.Abs(angleBetweenRotorAndThrustVec) > Math.PI / 2)
                {
                    angleBetweenRotorAndThrustVec = Math.PI / 2 * angleBetweenRotorAndThrustVec > 0 ? 1 : -1;
                }

                float ThrustPerThruster = (float)(thrustVec.Length() * Math.Pow(Math.Cos(angleBetweenRotorAndThrustVec), 2)) / MainThrusters.Count;

                MainThrusters.ForEach(thruster =>
                {
                    thruster.ThrustOverride = ThrustPerThruster;
                });
            }
        }
    }
}
