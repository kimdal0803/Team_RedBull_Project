using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x (Unity 6 기준)
// 만약 에러가 나면 using Cinemachine; 으로 바꿔주세요.

public class AutoCameraFollow : MonoBehaviour
{
    void Start()
    {
        // 씬에서 "Player" 태그를 가진 오브젝트를 자동으로 찾음
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var vcam = GetComponent<CinemachineCamera>(); // 유니티 6
            if (vcam != null)
            {
                vcam.Follow = player.transform;
                vcam.LookAt = player.transform;
            }
        }
    }
}
