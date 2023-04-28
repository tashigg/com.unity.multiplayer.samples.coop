using System;
using System.Threading.Tasks;
using Unity.BossRoom.UnityServices.Lobbies;
using Unity.Multiplayer.Samples.Utilities;
using Tashi.NetworkTransport;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to when a client is attempting to connect to a server. Starts the client when
    /// entering. If successful, transitions to the ClientConnected state. If not, transitions to the Offline state.
    /// </summary>
    class ClientConnectingState : OnlineState
    {
        ConnectionMethodBase m_ConnectionMethod;

        [Inject]
        protected LobbyServiceFacade m_LobbyServiceFacade;
        [Inject]
        protected LocalLobby m_LocalLobby;

        public ClientConnectingState Configure(ConnectionMethodBase baseConnectionMethod)
        {
            m_ConnectionMethod = baseConnectionMethod;
            return this;
        }

        public override void Enter()
        {
#pragma warning disable 4014
            ConnectClientAsync();
#pragma warning restore 4014

            // Clients need to be able to discover each other through the
            // Lobby's user data before they're considered connected.
            if (m_LobbyServiceFacade.CurrentUnityLobby != null)
            {
                m_LobbyServiceFacade.BeginTracking();
            }

            m_LocalLobby.changed += OnLobbyChanged;
        }

        public override void Exit() { }

        public override void OnClientConnected(ulong _)
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnected);
        }

        public override void OnClientDisconnect(ulong _)
        {
            // client ID is for sure ours here
            StartingClientFailedAsync();
        }

        protected void StartingClientFailedAsync()
        {
            var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
            if (string.IsNullOrEmpty(disconnectReason))
            {
                m_ConnectStatusPublisher.Publish(ConnectStatus.StartClientFailed);
            }
            else
            {
                var connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                m_ConnectStatusPublisher.Publish(connectStatus);
            }
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }


        internal async Task ConnectClientAsync()
        {
            try
            {
                // Setup NGO with current connection method
                await m_ConnectionMethod.SetupClientConnectionAsync();

                // NGO's StartClient launches everything
                if (!m_ConnectionManager.NetworkManager.StartClient())
                {
                    throw new Exception("NetworkManager StartClient failed");
                }

                SceneLoaderWrapper.Instance.AddOnSceneEventCallback();
            }
            catch (Exception e)
            {
                Debug.LogError("Error connecting client, see following exception");
                Debug.LogException(e);
                StartingClientFailedAsync();
                throw;
            }
        }

        private void OnLobbyChanged(LocalLobby lobby)
        {
            var transport = (TashiNetworkTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
            foreach (var user in m_LocalLobby.LobbyUsers.Values)
            {
                transport.AddAddressBookEntry(user.AddressBookEntry.GetValueOrDefault());
                if (user.IsHost)
                {
                    transport.HostPublicKey = user.AddressBookEntry?.PublicKey;
                }
            }
        }
    }
}
