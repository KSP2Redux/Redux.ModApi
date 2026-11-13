using System;
using System.Linq;
using KSP.Messages;
using Redux.Managers;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.Controllers
{
    public class ResourceScanDebugWindowController : BaseWindowController
    {
        private DropdownField _celestialBody;
        private DropdownField _resource;

        private ScrollView _scanned;
        private ScrollView _unscanned;

        private Button _scanUnscan;
        private Button _scanCBAll;
        private Button _unscanCBAll;
        private Button _scanAll;
        private Button _unscanAll;

        private bool _isCurrentResourceScanned;

        private void Awake()
        {
            // Persistent, so the window keeps working after a campaign is closed and another one loaded.
            Game.Messages.PersistentSubscribe<GameLoadFinishedMessage>(OnGameLoadFinished);
        }

        private void OnEnable()
        {
            Enable();
            
            _celestialBody = RootElement.Q<DropdownField>("celestial-body");
            _celestialBody.RegisterValueChangedCallback(CelestialBodyChanged);

            _resource = RootElement.Q<DropdownField>("resource");
            
            _scanned = RootElement.Q<ScrollView>("scanned");
            _unscanned = RootElement.Q<ScrollView>("unscanned");
            _scanned.Clear();
            _unscanned.Clear();

            _scanUnscan = RootElement.Q<Button>("scan-unscan");
            _scanUnscan.clicked += ScanUnscan;

            _scanCBAll = RootElement.Q<Button>("scan-cb-all");
            _scanCBAll.clicked += ScanCBAll;

            _unscanCBAll = RootElement.Q<Button>("unscan-cb-all");
            _unscanCBAll.clicked += UnscanCBAll;

            _scanAll = RootElement.Q<Button>("scan-all");
            _scanAll.clicked += ScanAll;

            _scanAll = RootElement.Q<Button>("unscan-all");
            _scanAll.clicked += UnscanAll;
        }

        private void OnDestroy()
        {
            if (IsGameShuttingDown || Game == null)
                return;

            Game.Messages.Unsubscribe<GameLoadFinishedMessage>(OnGameLoadFinished);
        }

        private void OnGameLoadFinished(MessageCenterMessage message)
        {
            if (message is not GameLoadFinishedMessage)
            {
                return;
            }

            PopulateBodies();
        }

        /// <inheritdoc />
        public override void CatchUpWithLoadedGame() => PopulateBodies();

        private void PopulateBodies()
        {
            var bodies = ISRUResourceManager.CbResources.Keys.ToList();
            _celestialBody.choices = bodies;
            if (bodies.Count == 0)
            {
                return;
            }

            // Start on the home world, unless the galaxy gives it no resources
            var homeWorld = Game.UniverseModel.HomeWorld?.bodyName;
            _celestialBody.value = bodies.Contains(homeWorld) ? homeWorld : bodies[0];
            PopulateResourceDropdown();
            PopulateScannedUnscanned();
        }

        private void PopulateResourceDropdown()
        {
            _resource.choices = ISRUResourceManager.CbResources[_celestialBody.value].Keys.ToList();
            _resource.value = ISRUResourceManager.CbResources[_celestialBody.value].Keys.First();
        }

        private void CelestialBodyChanged(ChangeEvent<string> evt)
        {
            PopulateResourceDropdown();
            PopulateScannedUnscanned();
        }

        private void PopulateScannedUnscanned()
        {
            _scanned.Clear();
            _unscanned.Clear();

            foreach (var resource in ISRUResourceManager.GetAllScannedResourcesFromCB(_celestialBody.value))
            {
                if (_resource.value == resource)
                {
                    _isCurrentResourceScanned = true;
                }
                
                var label = new Label { text = resource };
                label.AddToClassList("dt-table__row");
                _scanned.Add(label);
            }

            foreach (var resource in ISRUResourceManager.GetAllUnscannedResourcesFromCB(_celestialBody.value))
            {
                if (_resource.value == resource)
                {
                    _isCurrentResourceScanned = false;
                }
                
                var label = new Label { text = resource };
                label.AddToClassList("dt-table__row");
                _unscanned.Add(label);
            }
        }

        private void ScanUnscan()
        {
            _isCurrentResourceScanned = !_isCurrentResourceScanned;
            ISRUResourceManager.SetCBResourceScanned(_celestialBody.value, _resource.value, _isCurrentResourceScanned);
            PopulateScannedUnscanned();
        }

        private void ScanCBAll()
        {
            ISRUResourceManager.SetCBResourcesScanned(_celestialBody.value, _resource.choices, scanned: true);
            PopulateScannedUnscanned();
        }

        private void UnscanCBAll()
        {
            ISRUResourceManager.SetCBResourcesScanned(_celestialBody.value, _resource.choices, scanned: false);
            PopulateScannedUnscanned();
        }

        private void ScanAll()
        {
            ISRUResourceManager.ScanAllResources();
            PopulateScannedUnscanned();
        }

        private void UnscanAll()
        {
            ISRUResourceManager.UnscanAllResources();
            PopulateScannedUnscanned();
        }
    }
}