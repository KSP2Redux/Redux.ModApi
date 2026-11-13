// ReSharper disable UseObjectOrCollectionInitializer

using System.Collections.Generic;
using DebugTools.Runtime.UI.VesselTools;
using DebugTools.Utils;
using KSP;
using KSP.Sim;
using KSP.Sim.impl;
using UniLinq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers.VesselTools
{
    public class MassStatsWindowController : BaseWindowController
    {
        private ScrollView? _itemsView;
        private PartDetailsRow? _vesselRow;
        private List<PartDetailsRow>? _rows;

        private float _refreshRemaining;

        private void LateUpdate()
        {
            if (!IsWindowOpen)
                return;

            _refreshRemaining -= Time.unscaledDeltaTime;
            if (_refreshRemaining > 0f)
                return;
            _refreshRemaining = 1f;

            var view = Game.ViewController;
            if (view == null)
                return;

            var vessel = view.GetActiveSimVessel();
            if (vessel == null)
                return;

            var behavior = view.GetBehaviorIfLoaded(vessel);
            if (behavior == null)
                return;

            SyncTo(vessel, behavior);
        }

        private void OnEnable()
        {
            _rows = new List<PartDetailsRow>();
            
            _itemsView = RootElement.Q<ScrollView>("items-view");
            _itemsView.Clear();
            UITKHelper.SetTableHeader(new PartDetailsRow(true), RootElement.Q("header-host"), _itemsView);

            _vesselRow = new PartDetailsRow();
        }

        public void SyncTo(VesselComponent vessel, VesselBehavior behavior)
        {
            if (_itemsView == null || _rows == null || _vesselRow == null) return;

            var parts = behavior.parts.ToList();

            if (parts.Count != _rows.Count)
            {
                _itemsView.Clear();
                _rows.Clear();

                // The first row is the vessel total, set apart from the part rows by its style.
                _vesselRow = new PartDetailsRow(false);
                _vesselRow.AddToClassList("part-details--total");
                UITKHelper.ClearLabels(_vesselRow);
                _vesselRow.PartName.text = vessel.RevealDisplayName();
                _vesselRow.ModelMass.text = $"{vessel.totalMass:F2}";
                _itemsView.Add(_vesselRow);

                foreach (var part in behavior.parts)
                {
                    var row = new PartDetailsRow(false);
                    UITKHelper.ClearLabels(row);
                    FormatMassStats(row, part);
                    _itemsView.Add(row);
                    _rows.Add(row);
                }

                return;
            }

            _vesselRow.PartName.text = vessel.RevealDisplayName();
            _vesselRow.ModelMass.text = $"{vessel.totalMass:F2}";

            var i = 0;
            foreach (var part in behavior.parts)
                FormatMassStats(_rows[i++], part);
        }

        private static void FormatMassStats(PartDetailsRow row, PartBehavior part)
        {
            row.PartName.text = part.GetDisplayName();
            row.ModelMass.text = part.Model.PhysicsMass.ToString("0.00");
            row.PartMass.text = part.Model.DryMass.ToString("0.00");
            row.ResourceMass.text = part.Model.ResourceMass.ToString("0.00");
            row.GreenMass.text = part.Model.GreenMass.ToString("0.00");
            if (part.Rigidbody != null && part.Rigidbody.activeRigidBody != null)
            {
                row.RBMass.text = part.Rigidbody.mass.ToString("0.00");
                row.RBPhysXMass.text = part.Rigidbody.PhysicsMode == PartPhysicsModes.None
                    ? "0.00"
                    : part.Rigidbody.activeRigidBody.mass.ToString("0.00");
            }

            row.WettedArea.text = $"{part.Model.WettedArea:F2} {Units.SymbolMetersSquared}";
        }
    }
}