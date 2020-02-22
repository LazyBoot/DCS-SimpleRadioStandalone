﻿using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network
{
    public class SRClient : INotifyPropertyChanged
    {
        //  public DcsPosition Position { get; set; }

        private int _coalition;

        [JsonIgnore] private float _lineOfSightLoss; // 0.0 is NO Loss therefore Full line of sight

        public string ClientGuid { get; set; }

        public string Name { get; set; }


        public int Coalition
        {
            get { return _coalition; }
            set
            {
                _coalition = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Coalition"));
            }
        }

        [JsonIgnore]
        public bool Muted { get; set; }

        [JsonIgnore]
        public IPEndPoint VoipPort { get; set; }

        [JsonIgnore]
        public long LastUpdate { get; set; }

        [JsonIgnore]
        public long LastRadioUpdateSent { get; set; }

        public DCSPlayerRadioInfo RadioInfo { get; set; }

        public DcsPosition Position { get; set; } = new DcsPosition();

        public DCSLatLngPosition LatLngPosition { get; set; }

        [JsonIgnore]
        public float LineOfSightLoss
        {
            get
            {
                if (_lineOfSightLoss == 0)
                {
                    return 0;
                }
                if ((LatLngPosition.lat == 0) && (LatLngPosition.lng == 0))
                {
                    return 0;
                }
                return _lineOfSightLoss;
            }
            set { _lineOfSightLoss = value; }
        }

        public string ClientChannelId { get; set; }

        // Used by server client list to display last frequency client transmitted on
        private string _transmittingFrequency;
        [JsonIgnore]
        public string TransmittingFrequency
        {
            get { return _transmittingFrequency; }
            set
            {
                if (_transmittingFrequency != value)
                {
                    _transmittingFrequency = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TransmittingFrequency"));
                }
            }
        }

        // Used by server client list to remove last frequency client transmitted on after threshold
        [JsonIgnore]
        public DateTime LastTransmissionReceived { get; set; }

        //is an SRSClientSession but dont want to include the dependancy for now
        [JsonIgnore]
        public object ClientSession { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;


        public bool isCurrent()
        {
            return true;
//             if(LastUpdate > DateTime.Now.Ticks - 100000000)//last in game 10 seconds ago
//             {
//                Console.WriteLine("NOT CURRENT!");
//                 return true;
//             }
//            else
//            {
//                return true;
//            }
        }

        public bool IsIngame()
        {
            // Clients are counted as ingame if they have a name and have been updated within the last 10 seconds
            return !string.IsNullOrEmpty(Name) && DateTime.Now.Ticks - LastUpdate < 100000000;
        }

        public override string ToString()
        {
            string side;

            if (Coalition == 1)
            {
                side = "Red";
            }
            else if (Coalition == 2)
            {
                side = "Blue";
            }
            else
            {
                side = "Spectator";
            }
            return Name == "" ? "Unknown" : Name + " - " + side + " LOS Loss " + _lineOfSightLoss + " Pos" + LatLngPosition;
        }
    }
}