using System;
using System.Collections.Generic;
using System.Net;
using JetBrains.Annotations;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Tashi.ConsensusEngine;

namespace Unity.BossRoom.UnityServices.Lobbies
{
    /// <summary>
    /// Data for a local lobby user instance. This will update data and is observed to know when to push local user changes to the entire lobby.
    /// </summary>
    [Serializable]
    public class LocalLobbyUser
    {
        public event Action<LocalLobbyUser> changed;

        public LocalLobbyUser()
        {
            m_UserData = new UserData(isHost: false, displayName: null, id: null, addressBookEntry: null);
        }

        public struct UserData
        {
            public bool IsHost { get; set; }
            public string DisplayName { get; set; }
            public string ID { get; set; }
            public AddressBookEntry? AddressBookEntry { get; set; }

            public UserData(bool isHost, string displayName, string id, AddressBookEntry? addressBookEntry)
            {
                IsHost = isHost;
                DisplayName = displayName;
                ID = id;
                AddressBookEntry = addressBookEntry;
            }
        }

        UserData m_UserData;

        public void ResetState()
        {
            m_UserData = new UserData(false, m_UserData.DisplayName, m_UserData.ID, m_UserData.AddressBookEntry);
        }

        /// <summary>
        /// Used for limiting costly OnChanged actions to just the members which actually changed.
        /// </summary>
        [Flags]
        public enum UserMembers
        {
            IsHost = 1,
            DisplayName = 2,
            ID = 4,
            AddressBookEntry = 5,
        }

        UserMembers m_LastChanged;

        public bool IsHost
        {
            get { return m_UserData.IsHost; }
            set
            {
                if (m_UserData.IsHost != value)
                {
                    m_UserData.IsHost = value;
                    m_LastChanged = UserMembers.IsHost;
                    OnChanged();
                }
            }
        }

        public string DisplayName
        {
            get => m_UserData.DisplayName;
            set
            {
                if (m_UserData.DisplayName != value)
                {
                    m_UserData.DisplayName = value;
                    m_LastChanged = UserMembers.DisplayName;
                    OnChanged();
                }
            }
        }

        public string ID
        {
            get => m_UserData.ID;
            set
            {
                if (m_UserData.ID != value)
                {
                    m_UserData.ID = value;
                    m_LastChanged = UserMembers.ID;
                    OnChanged();
                }
            }
        }

        public AddressBookEntry? AddressBookEntry
        {
            get => m_UserData.AddressBookEntry;
            set
            {
                if (!Equals(m_UserData.AddressBookEntry, value))
                {
                    m_UserData.AddressBookEntry = value;
                    m_LastChanged = UserMembers.AddressBookEntry;
                    OnChanged();
                }
            }
        }

        public void CopyDataFrom(LocalLobbyUser lobby)
        {
            var data = lobby.m_UserData;
            int lastChanged = // Set flags just for the members that will be changed.
                (m_UserData.IsHost == data.IsHost ? 0 : (int)UserMembers.IsHost) |
                (m_UserData.DisplayName == data.DisplayName ? 0 : (int)UserMembers.DisplayName) |
                (m_UserData.ID == data.ID ? 0 : (int)UserMembers.ID) |
                (Equals(m_UserData.AddressBookEntry, data.AddressBookEntry) ? 0 : (int)UserMembers.AddressBookEntry);

            if (lastChanged == 0) // Ensure something actually changed.
            {
                return;
            }

            m_UserData = data;
            m_LastChanged = (UserMembers)lastChanged;

            OnChanged();
        }

        void OnChanged()
        {
            changed?.Invoke(this);
        }

        public Dictionary<string, PlayerDataObject> GetDataForUnityServices()
        {
            var result = new Dictionary<string, PlayerDataObject>()
            {
                { "DisplayName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, DisplayName) },
            };

            if (AddressBookEntry is not null)
            {
                result.Add(
                    "PublicKey",
                    new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member,
                        Convert.ToBase64String(AddressBookEntry?.PublicKey.AsDer()))
                );

                result.Add(
                    "BoundAddress",
                    new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, AddressBookEntry?.Address.Address.ToString()));

                result.Add(
                    "BoundPort",
                    new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, AddressBookEntry?.Address.Port.ToString())
                );
            }

            return result;
        }
    }
}
