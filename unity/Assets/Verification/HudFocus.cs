using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
// All controls remain visible; keyboard/controller selection only changes the focus outline.
public sealed class HudFocus : MonoBehaviour,ISelectHandler,IDeselectHandler
{
    public Outline outline;
    public void OnSelect(BaseEventData eventData){if(outline!=null)outline.enabled=true;}
    public void OnDeselect(BaseEventData eventData){if(outline!=null)outline.enabled=false;}
}
