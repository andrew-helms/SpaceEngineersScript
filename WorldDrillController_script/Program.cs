using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Configuration;
using System.Linq;
using System.Net;
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
using static IngameScript.Program.WorldDrill;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        WorldDrill Drill;

        public Program()
        {
            MyIni ini = new MyIni();
            MyIniParseResult result;

            if (!ini.TryParse(Me.CustomData, out result))
            {
                throw new Exception(result.ToString());
            }

            string[] blockMappings = Me.CustomData.Split('\n');

            List<IMyExtendedPistonBase> UpPistons = new List<IMyExtendedPistonBase>();
            List<IMyExtendedPistonBase> DownPistons = new List<IMyExtendedPistonBase>();
            List<IMyExtendedPistonBase> ForwardPistons = new List<IMyExtendedPistonBase>();
            List<IMyExtendedPistonBase> BackwardPistons = new List<IMyExtendedPistonBase>();
            List<IMyExtendedPistonBase> RightPistons = new List<IMyExtendedPistonBase>();
            List<IMyExtendedPistonBase> LeftPistons = new List<IMyExtendedPistonBase>();
            List<IMyExtendedPistonBase> Pistons = new List<IMyExtendedPistonBase>();

            List<IMyShipDrill> Drills = new List<IMyShipDrill>();

            IMyMotorAdvancedStator Rotor = null;

            IMySensorBlock GroundSensor = null;

            float DrillSpeed = ini.Get("WorldDrill", nameof(DrillSpeed)).ToSingle(0.01f);
            float MoveSpeed = ini.Get("WorldDrill", nameof(MoveSpeed)).ToSingle(1f);
            float RowColSpacing = ini.Get("WorldDrill", nameof(RowColSpacing)).ToSingle(3.33f);
            float StartRow = ini.Get("WorldDrill", nameof(StartRow)).ToSingle(0f);
            float StartCol = ini.Get("WorldDrill", nameof(StartCol)).ToSingle(0f);
            float StartDepth = ini.Get("WorldDrill", nameof(StartDepth)).ToSingle(0f);
            float PistonTolerance = ini.Get("WorldDrill", nameof(PistonTolerance)).ToSingle(0.01f);

            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            string search = ini.Get("WorldDrill", nameof(UpPistons)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                UpPistons = blocks.ConvertAll(block => block as IMyExtendedPistonBase).Where(piston => piston != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(DownPistons)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                DownPistons = blocks.ConvertAll(block => block as IMyExtendedPistonBase).Where(piston => piston != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(RightPistons)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                RightPistons = blocks.ConvertAll(block => block as IMyExtendedPistonBase).Where(piston => piston != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(LeftPistons)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                LeftPistons = blocks.ConvertAll(block => block as IMyExtendedPistonBase).Where(piston => piston != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(ForwardPistons)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                ForwardPistons = blocks.ConvertAll(block => block as IMyExtendedPistonBase).Where(piston => piston != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(BackwardPistons)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                BackwardPistons = blocks.ConvertAll(block => block as IMyExtendedPistonBase).Where(piston => piston != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(Pistons)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                Pistons = blocks.ConvertAll(block => block as IMyExtendedPistonBase).Where(piston => piston != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(Drills)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                Drills = blocks.ConvertAll(block => block as IMyShipDrill).Where(drill => drill != null).ToList();
            }

            search = ini.Get("WorldDrill", nameof(Rotor)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                Rotor = blocks.ConvertAll(block => block as IMyMotorAdvancedStator).FirstOrDefault(rotor => rotor != null);
            }

            search = ini.Get("WorldDrill", nameof(GroundSensor)).ToString();
            if (!string.IsNullOrEmpty(search))
            {
                GridTerminalSystem.SearchBlocksOfName(search, blocks);
                GroundSensor = blocks.ConvertAll(block => block as IMySensorBlock).FirstOrDefault(gs => gs != null);
            }

            Echo(Pistons.Count.ToString());

            string test = "";

            MatrixD reference = Drills.First().WorldMatrix;
            // TEST THIS
            Pistons.ForEach(piston =>
            {
                if (piston == null)
                {
                    test += "null\n";
                }
                else
                {
                    Vector3D relativeRotation = Vector3D.TransformNormal(piston.WorldMatrix.Up, MatrixD.Transpose(reference));

                    test += $"{relativeRotation.X},{relativeRotation.Y},{relativeRotation.Z}:";

                    if (relativeRotation.Y > 0.99)
                    {
                        test += "right";//m_ZPistons.Add(new PistonWithDirection(piston, false));
                    }
                    else if (relativeRotation.Y < -0.99)
                    {
                        test += "left";//m_ZPistons.Add(new PistonWithDirection(piston, true));
                    }
                    else if (relativeRotation.X > 0.99)
                    {
                        test += "backward";//m_XPistons.Add(new PistonWithDirection(piston, false));
                    }
                    else if (relativeRotation.X < -0.99)
                    {
                        test += "forward";//m_XPistons.Add(new PistonWithDirection(piston, true));
                    }
                    else if (relativeRotation.Z > 0.99)
                    {
                        test += "up";//m_YPistons.Add(new PistonWithDirection(piston, false));
                    }
                    else if (relativeRotation.Z < -0.99)
                    {
                        test += "down";//m_YPistons.Add(new PistonWithDirection(piston, true));
                    }
                    test += "\n";
                }
            });

            Echo(test);

            Drill = new WorldDrill(drills: Drills, pistons: Pistons,
            drillSpeed: DrillSpeed, moveSpeed: MoveSpeed, rowColSpacing: RowColSpacing, pistonTolerance: PistonTolerance,
            startDepth: StartDepth, startRow: StartRow, startCol: StartCol, rotor: Rotor, groundSensor: GroundSensor);

            //Drill = new WorldDrill(drills: Drills, drillSpeed: DrillSpeed, moveSpeed: MoveSpeed, rowColSpacing: RowColSpacing, pistonTolerance: PistonTolerance,
            //    startDepth: StartDepth, startRow: StartRow, startCol: StartCol, rotor: Rotor, groundSensor: GroundSensor, upPistons: UpPistons, downPistons: DownPistons,
            //    rightPistons: RightPistons, leftPistons: LeftPistons, forwardPistons: ForwardPistons, backwardPistons: BackwardPistons);

            //Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Update100) != 0)
            {
                if (!Drill.IsFinished)
                {
                    Drill.Update();
                    Echo(Drill.CurrentState);
                }
            }
        }
    }
}
