using System.Collections.Generic;
using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
	private const int SEND_UPDATES_PER_SEC = 20;
	private const float SEND_UPDATES_FREQUENCY = 1f / SEND_UPDATES_PER_SEC;

	public static PlayerController Local { get; private set; }

	private uint PlayerId;
	private float LastMovementSendTimestamp;
	private Vector2? LockInputPosition;
	private List<CircleController> OwnedCircles = new List<CircleController>();

	public string Username => GameManager.Conn.Db.Player.PlayerId.Find(PlayerId).Name;
	public int NumberOfOwnedCircles => OwnedCircles.Count;
	public bool IsLocalPlayer => this == Local;

	public void Initialize(Player player)
    {
        PlayerId = player.PlayerId;
        if (player.Identity == GameManager.LocalIdentity)
        {
            Local = this;
        }
	}

    private void OnDestroy()
    {
        // If we have any circles, destroy them
        foreach (var circle in OwnedCircles)
        {
            if (circle != null)
            {
                Destroy(circle.gameObject);
            }
        }
        OwnedCircles.Clear();
    }

    public void OnCircleSpawned(CircleController circle)
    {
        OwnedCircles.Add(circle);
    }

    public void OnCircleDeleted(CircleController deletedCircle)
	{
		// This means we got eaten
		if (OwnedCircles.Remove(deletedCircle) && IsLocalPlayer && OwnedCircles.Count == 0)
		{
			// DeathScreen.Instance.SetVisible(true);
		}
	}

	public uint TotalMass()
	{
		// If this entity is being deleted on the same frame that we're moving, we can have a null entity here.
		return (uint)OwnedCircles
			.Select(circle => GameManager.Conn.Db.Entity.EntityId.Find(circle.EntityId))
			.Sum(e => e?.Mass ?? 0);
	}

    public Vector2? CenterOfMass()
    {
        if (OwnedCircles.Count == 0)
        {
            return null;
        }
        
        Vector2 totalPos = Vector2.zero;
        float totalMass = 0;
        foreach (var circle in OwnedCircles)
        {
            var entity = GameManager.Conn.Db.Entity.EntityId.Find(circle.EntityId);
            var position = circle.transform.position;
            totalPos += (Vector2)position * entity.Mass;
            totalMass += entity.Mass;
        }

        return totalPos / totalMass;
	}

	private void OnGUI()
	{
		if (!IsLocalPlayer || !GameManager.IsConnected())
		{
			return;
		}

		GUI.Label(new Rect(0, 0, 100, 50), $"Total Mass: {TotalMass()}");
	}

	//Automated testing members
	private bool testInputEnabled;
	private Vector2 testInput;

	public void SetTestInput(Vector2 input) => testInput = input;
	public void EnableTestInput() => testInputEnabled = true;
}