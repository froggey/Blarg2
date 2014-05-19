using UnityEngine;
using System.Collections;

public class SpawnPoint : MonoBehaviour {
        public string entityName;
        public int team;
        public enum Action {
                NONE, MOVE
        }
        public Action action = Action.NONE;
        public Vector2 point;

        void OnDrawGizmos() {
                Gizmos.DrawIcon(transform.position, "SpawnPoint.png", true);
        }

        void Start() {
                DVector2 vec = new DVector2((DReal)transform.position.z, (DReal)transform.position.x);
                DReal rotation = DReal.Radians((DReal)transform.rotation.eulerAngles.y);
                Entity ent = ComSat.Spawn(entityName, team, vec, rotation);
                if(ent == null) return;
                if(action == Action.MOVE) {
                        ComSat.IssueMove(ent, new DVector2((DReal)point.x, (DReal)point.y));
                }
        }
}
