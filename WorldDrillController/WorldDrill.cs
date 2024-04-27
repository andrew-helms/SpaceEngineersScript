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

namespace IngameScript
{
    partial class Program
    {
        public class WorldDrill
        {
            private List<PistonWithDirection> m_ZPistons;
            private List<PistonWithDirection> m_XPistons;
            private List<PistonWithDirection> m_YPistons;

            private List<IMyShipDrill> m_Drills;

            private IMyMotorAdvancedStator m_Rotor;

            private IMySensorBlock m_GroundSensor;

            private float m_DrillSpeed;
            private float m_MoveSpeed;
            private float m_RowColSpacing;
            private float m_StartDepth;
            private float m_PistonTolerance;
            
            private State m_State;
            private float m_CurrCol;
            private float m_CurrRow;

            public bool IsFinished { get { return m_State == State.Done; } }

            public string CurrentState { get { return m_State.ToString(); } }

            enum State
            {
                SetupStart,
                RetractingStart,
                MovingToStart,
                ExtendingToStart,
                Drilling,
                Retracting,
                MovingGantry,
                Done,
            }

            internal struct PistonWithDirection
            {
                internal IMyExtendedPistonBase Piston;
                internal bool Inverted;

                internal float Position { get { return Inverted ? 10f - Piston.CurrentPosition : Piston.CurrentPosition; } }

                internal PistonWithDirection(IMyExtendedPistonBase piston, bool inverted)
                {
                    Piston = piston;
                    Inverted = inverted;
                }
            }

            public WorldDrill(List<IMyShipDrill> drills, float drillSpeed, float moveSpeed, float rowColSpacing, float pistonTolerance = 0f, float startDepth = 0f,
                       float startRow = 0, float startCol = 0, IMyMotorAdvancedStator rotor = null, IMySensorBlock groundSensor = null,
                       List<IMyExtendedPistonBase> upPistons = null, List<IMyExtendedPistonBase> downPistons = null,
                       List<IMyExtendedPistonBase> rightPistons = null, List<IMyExtendedPistonBase> leftPistons = null,
                       List<IMyExtendedPistonBase> forwardPistons = null, List<IMyExtendedPistonBase> backwardPistons = null)
            {
                m_Drills = drills;
                m_Rotor = rotor;
                m_GroundSensor = groundSensor;

                m_ZPistons = upPistons.ConvertAll(piston => new PistonWithDirection(piston, true)) ?? new List<PistonWithDirection>();
                m_ZPistons.AddRange(downPistons.ConvertAll(piston => new PistonWithDirection(piston, false)) ?? new List<PistonWithDirection>());
                m_XPistons = rightPistons.ConvertAll(piston => new PistonWithDirection(piston, false)) ?? new List<PistonWithDirection>();
                m_XPistons.AddRange(leftPistons.ConvertAll(piston => new PistonWithDirection(piston, true)) ?? new List<PistonWithDirection>());
                m_YPistons = forwardPistons.ConvertAll(piston => new PistonWithDirection(piston, false)) ?? new List<PistonWithDirection>();
                m_YPistons.AddRange(backwardPistons.ConvertAll(piston => new PistonWithDirection(piston, true)) ?? new List<PistonWithDirection>());

                m_DrillSpeed = drillSpeed;
                m_MoveSpeed = moveSpeed;
                m_RowColSpacing = rowColSpacing;
                m_PistonTolerance = pistonTolerance;
                m_StartDepth = startDepth;
                m_CurrRow = startRow;
                m_CurrCol = startCol;

                m_State = State.SetupStart;
            }

            public WorldDrill(List<IMyShipDrill> drills, List<IMyExtendedPistonBase> pistons, float drillSpeed, float moveSpeed, float rowColSpacing, float pistonTolerance = 0f,
                float startDepth = 0f, float startRow = 0, float startCol = 0, IMyMotorAdvancedStator rotor = null, IMySensorBlock groundSensor = null)
            {
                m_Drills = drills;
                m_Rotor = rotor;
                m_GroundSensor = groundSensor;

                m_ZPistons = new List<PistonWithDirection>();
                m_XPistons = new List<PistonWithDirection>();
                m_YPistons = new List<PistonWithDirection>();

                MatrixD reference = drills.First().WorldMatrix;
                // TEST THIS
                pistons.ForEach(piston =>
                {
                    Vector3D relativeRotation = Vector3D.TransformNormal(piston.WorldMatrix.Up, MatrixD.Transpose(reference));

                    if (relativeRotation.Y > 0.99)
                    {
                        m_XPistons.Add(new PistonWithDirection(piston, false));
                    }
                    else if (relativeRotation.Y < -0.99)
                    {
                        m_XPistons.Add(new PistonWithDirection(piston, true));
                    }
                    else if (relativeRotation.X > 0.99)
                    {
                        m_YPistons.Add(new PistonWithDirection(piston, true));
                    }
                    else if (relativeRotation.X < -0.99)
                    {
                        m_YPistons.Add(new PistonWithDirection(piston, false));
                    }
                    else if (relativeRotation.Z > 0.99)
                    {
                        m_ZPistons.Add(new PistonWithDirection(piston, true));
                    }
                    else if (relativeRotation.Z < -0.99)
                    {
                        m_ZPistons.Add(new PistonWithDirection(piston, false));
                    }
                });

                //m_ZPistons = upPistons.ConvertAll(piston => new PistonWithDirection(piston, true)) ?? new List<PistonWithDirection>();
                //m_ZPistons.AddRange(downPistons.ConvertAll(piston => new PistonWithDirection(piston, false)) ?? new List<PistonWithDirection>());
                //m_XPistons = rightPistons.ConvertAll(piston => new PistonWithDirection(piston, false)) ?? new List<PistonWithDirection>();
                //m_XPistons.AddRange(leftPistons.ConvertAll(piston => new PistonWithDirection(piston, true)) ?? new List<PistonWithDirection>());
                //m_YPistons = forwardPistons.ConvertAll(piston => new PistonWithDirection(piston, false)) ?? new List<PistonWithDirection>();
                //m_YPistons.AddRange(backwardPistons.ConvertAll(piston => new PistonWithDirection(piston, true)) ?? new List<PistonWithDirection>());

                m_DrillSpeed = drillSpeed;
                m_MoveSpeed = moveSpeed;
                m_RowColSpacing = rowColSpacing;
                m_PistonTolerance = pistonTolerance;
                m_StartDepth = startDepth;
                m_CurrRow = startRow;
                m_CurrCol = startCol;

                m_State = State.SetupStart;
            }

            public void Update()
            {
                switch (m_State)
                {
                    case State.SetupStart:
                        {
                            SetPistonSpeed(m_ZPistons, -m_MoveSpeed);

                            m_State = State.RetractingStart;                             

                            break;
                        }
                    case State.RetractingStart:
                        {
                            if (AllPistonsRetracted(m_ZPistons))
                            {
                                SetPistonLimits(m_XPistons, m_CurrCol, m_CurrCol);
                                SetPistonLimits(m_YPistons, m_CurrRow, m_CurrRow);

                                if (AllPistonsRetracted(m_XPistons))
                                {
                                    SetPistonSpeed(m_XPistons, m_MoveSpeed);
                                }
                                else
                                {
                                    SetPistonSpeed(m_XPistons, -m_MoveSpeed);
                                }

                                if (AllPistonsRetracted(m_YPistons))
                                {
                                    SetPistonSpeed(m_YPistons, m_MoveSpeed);
                                }
                                else
                                {
                                    SetPistonSpeed(m_YPistons, -m_MoveSpeed);
                                }

                                m_State = State.MovingToStart;
                            }
                            break;
                        }
                    case State.MovingToStart:
                        {
                            if (AllPistonsExtended(m_XPistons) && AllPistonsExtended(m_YPistons) && AllPistonsRetracted(m_XPistons) && AllPistonsRetracted(m_YPistons))
                            {
                                SetPistonLimits(m_ZPistons, 0f, m_StartDepth);
                                SetPistonSpeed(m_ZPistons, m_MoveSpeed);

                                m_State = State.ExtendingToStart;
                            }
                        
                            break;
                        }
                    case State.ExtendingToStart:
                        {
                            if (AllPistonsExtended(m_ZPistons))
                            {
                                SetPistonSpeed(m_ZPistons, m_DrillSpeed);
                                SetPistonLimits(m_ZPistons, 0f, m_ZPistons.Count * 10f);
                                m_Drills.ForEach(drill => drill.Enabled = true);

                                m_State = State.Drilling;
                            }
                            break;
                        }
                    case State.Drilling:
                        {
                            if (AllPistonsExtended(m_ZPistons))
                            {
                                //Echo($"Finished drilling at row:{m_CurrRow}, col:{m_CurrCol}");

                                SetPistonSpeed(m_ZPistons, -m_MoveSpeed);

                                m_State = State.Retracting;
                            }
                            break;
                        }
                    case State.Retracting:
                        {
                            if (AllPistonsRetracted(m_ZPistons))
                            {
                                //Echo($"Finished retracting drill at row:{m_CurrRow}, col:{m_CurrCol}");

                                m_CurrCol += m_RowColSpacing;

                                if (m_CurrCol > 10f * m_XPistons.Count)
                                {
                                    //Echo($"Finished with row:{m_CurrRow}");

                                    m_CurrCol = 0f;

                                    m_CurrRow += m_RowColSpacing;

                                    if (m_CurrRow > 10f * m_YPistons.Count)
                                    {
                                        //Echo($"Finished drilling, moving back to home");

                                        m_CurrRow = 0f;
                                    }
                                }

                                m_State = State.MovingGantry;
                            }
                            break;
                        }
                    case State.MovingGantry:
                        {
                            if (m_CurrCol == 0 && m_CurrRow == 0)
                            {
                                SetPistonSpeed(m_YPistons, -m_MoveSpeed);
                                SetPistonSpeed(m_XPistons, -m_MoveSpeed);

                                m_Drills.ForEach(drill => drill.Enabled = false);
                                if (m_Rotor != null)
                                {
                                    m_Rotor.Enabled = false;
                                }

                                m_State = State.Done;
                                break;
                            }

                            if (m_CurrCol == 0 && AllPistonsRetracted(m_XPistons))
                            {
                                SetPistonLimits(m_XPistons, 0f, 0f);
                                SetPistonSpeed(m_XPistons, -m_MoveSpeed);
                            }
                            else if (m_CurrCol > m_XPistons.Sum(piston => piston.Position) + m_PistonTolerance)
                            {
                                //Echo($"Moving to col:{m_CurrCol}");

                                SetPistonLimits(m_XPistons, 0f, m_CurrCol);
                                SetPistonSpeed(m_XPistons, m_MoveSpeed);
                            }
                            else if (m_CurrRow > m_YPistons.Sum(piston => piston.Position) + m_PistonTolerance)
                            {
                                //Echo($"Moving to row:{m_CurrRow}");

                                SetPistonLimits(m_YPistons, 0f, m_CurrRow);
                                SetPistonSpeed(m_YPistons, m_MoveSpeed);
                            }
                            else //In position to drill
                            {
                               //Echo($"Starting to drill at row{CurrRow}, col{CurrCol}");

                                SetPistonSpeed(m_ZPistons, m_DrillSpeed);

                                m_State = State.Drilling;
                            }

                            break;
                        }
                    case State.Done:
                        {
                            //Echo("Done!");
                            break;
                        }
                }
            }

            private bool AllPistonsExtended(List<PistonWithDirection> pistons)
            {
                return pistons.All(piston => piston.Inverted ? piston.Piston.CurrentPosition < (piston.Piston.MinLimit + m_PistonTolerance) : piston.Piston.CurrentPosition > (piston.Piston.MaxLimit - m_PistonTolerance));
            }

            private bool AllPistonsRetracted(List<PistonWithDirection> pistons)
            {
                return pistons.All(piston => piston.Inverted ? piston.Piston.CurrentPosition > (piston.Piston.MaxLimit - m_PistonTolerance) : piston.Piston.CurrentPosition < (piston.Piston.MinLimit + m_PistonTolerance));
            }

            private void SetPistonSpeed(List<PistonWithDirection> pistons, float combinedSpeed)
            {
                pistons.ForEach(piston => piston.Piston.Velocity = (piston.Inverted ? -1 : 1) * combinedSpeed / pistons.Count);
            }

            private void SetPistonLimits(List<PistonWithDirection> pistons, float minLimitCombined, float maxLimitCombined)
            {
                pistons.ForEach(piston =>
                {
                    piston.Piston.MinLimit = (piston.Inverted ? 10f - (maxLimitCombined / pistons.Count) : minLimitCombined / pistons.Count);
                    piston.Piston.MaxLimit = (piston.Inverted ? 10f - (minLimitCombined / pistons.Count) : maxLimitCombined / pistons.Count);
                });
            }
        }
    }
}
