using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionObject_CarDeliveryZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Car_Controller car = other.GetComponent<Car_Controller>();
        if (car == null)
            return;

        MissionObject_CarToDeliver carMission = car.GetComponent<MissionObject_CarToDeliver>();
        if (carMission == null)
            return;

        if (GameSessionData.IsCoopSession)
        {
            if (CoopNetworkManager.Instance.IsHosting)
                carMission.InvokeOnCarDelivery();
            else
                CoopNetworkManager.Instance.SendCoopCarDelivered(car.transform.position);

            return;
        }

        carMission.InvokeOnCarDelivery();
    }
}
