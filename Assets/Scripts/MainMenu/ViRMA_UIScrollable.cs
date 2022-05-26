﻿using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class ViRMA_UIScrollable : MonoBehaviour
{
    private ViRMA_GlobalsAndActions globals;
    private ScrollRect scrollRect;  
    private Transform scrollContent;
    private bool allowScrolling;

    private void Awake()
    {
        globals = Player.instance.gameObject.GetComponent<ViRMA_GlobalsAndActions>();
        scrollRect = GetComponent<ScrollRect>();
        scrollContent = scrollRect.content.transform;
    }

    private void Start()
    {
        SetMenuColliderSize();
    }

    private void Update()
    {

        ScrollableColliderController();

        EnableJoystickTouchScrolling();
    }

    private void OnTriggerStay(Collider triggeredCol)
    {
        if (triggeredCol.GetComponent<ViRMA_Drumstick>())
        {
            allowScrolling = true;
        }
    }

    private void OnTriggerExit(Collider triggeredCol)
    {
        if (triggeredCol.GetComponent<ViRMA_Drumstick>())
        {
            allowScrolling = false;
        }
    }

    private void ScrollableColliderController()
    {
        for (int i = 0; i < scrollContent.childCount; i++)
        {
            Transform child = scrollContent.GetChild(i);
            BoxCollider childCol = child.GetComponent<BoxCollider>();

            if (childCol)
            {
                Transform grandParent = child.transform.parent.parent;
                Vector3 positionRelativeToCenter = grandParent.InverseTransformPoint(child.position);
                float maxScrollingDist = grandParent.GetComponent<RectTransform>().sizeDelta.y / 2;
                float yDist = positionRelativeToCenter.y;

                if (yDist > maxScrollingDist || yDist < (maxScrollingDist * -1))
                {
                    childCol.enabled = false;
                }
                else
                {
                    childCol.enabled = true;
                }
            }       
        }
    }
    private void EnableJoystickTouchScrolling()
    {
        if (allowScrolling)
        {
            float joyStickDirection = globals.menuInteraction_Scroll.GetAxis(SteamVR_Input_Sources.Any).y;
            if (joyStickDirection != 0)
            {
                float multiplier = joyStickDirection * 0.45f;
                scrollContent.position -= multiplier * Time.deltaTime * transform.up;
            }
        }
    }
    private void SetMenuColliderSize()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;
        BoxCollider menuCol = GetComponent<BoxCollider>();
        menuCol.size = new Vector3(width, height, 25);
    }

  
}
