using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private const string SERVER_URL = "http://127.0.0.1:3000";
    private const string MODULE_NAME = "blackholio";

    public static event Action OnConnected;
    public static event Action OnSubscriptionApplied;

    public float borderThickness = 2;
    public Material borderMaterial;

    public static GameManager Instance { get; private set; }
    public static Identity LocalIdentity { get; private set; }
    public static DbConnection Conn { get; private set; }

    private void Start()
    {
        Instance = this;
        Application.targetFrameRate = 60;

        // In order to build a connection to SpacetimeDB we need to register
        // our callbacks and specify a SpacetimeDB server URI and module name.
        var builder = DbConnection.Builder()
            .OnConnect(HandleConnect)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnect)
            .WithUri(SERVER_URL)
            .WithModuleName(MODULE_NAME);

        // If the user has a SpacetimeDB auth token stored in the Unity PlayerPrefs,
        // we can use it to authenticate the connection.
		if (AuthToken.Token != "")
        {
            builder = builder.WithToken(AuthToken.Token);
        }

        // Building the connection will establish a connection to the SpacetimeDB
        // server.
        Conn = builder.Build();
    }

    // Called when we connect to SpacetimeDB and receive our client identity
    private void HandleConnect(DbConnection conn, Identity identity, string token)
    {
        Debug.Log("Connected.");
        AuthToken.SaveToken(token);
        LocalIdentity = identity;

        OnConnected?.Invoke();

        // Request all tables
        Conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .SubscribeToAllTables();
    }

    private void HandleConnectError(Exception ex)
    {
        Debug.LogError($"Connection error: {ex}");
    }

    private void HandleDisconnect(DbConnection _conn, Exception ex)
    {
        Debug.Log("Disconnected.");
        if (ex != null)
        {
            Debug.LogException(ex);
        }
    }

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("Subscription applied!");
        OnSubscriptionApplied?.Invoke();
    }

    public static bool IsConnected()
    {
        return Conn != null && Conn.IsActive;
    }

    public void Disconnect()
    {
        Conn.Disconnect();
        Conn = null;
    }
}