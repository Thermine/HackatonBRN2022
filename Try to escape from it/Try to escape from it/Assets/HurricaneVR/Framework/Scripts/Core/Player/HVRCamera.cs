using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HurricaneVR.Framework.Shared;
using UnityEngine;

namespace HurricaneVR.Framework.Core.Player
{
    [RequireComponent(typeof(Camera))]
    public class HVRCamera : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        
        
        private void Awake()
        {
            Camera = GetComponent<Camera>();

            gameObject.layer = LayerMask.NameToLayer(HVRLayers.Player.ToString());

        }
    }
}
