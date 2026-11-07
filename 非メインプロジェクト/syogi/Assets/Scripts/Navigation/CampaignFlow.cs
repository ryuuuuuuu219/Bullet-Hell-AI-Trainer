using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Shared menu, narrative handoff and debriefing. Story prose is referenced from Assets/nobel.
public sealed class CampaignFlow : MonoBehaviour
{
    public Font japaneseFont;
    [SerializeField] private TextAsset[] novelTexts;
    private void Start()
    {
        string scene=SceneManager.GetActiveScene().name;
        foreach(var canvas in FindObjectsByType<Canvas>()) canvas.gameObject.SetActive(false);
        if(EventSystem.current==null) new GameObject("Campaign Events",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var root=new GameObject("Campaign UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scale=root.GetComponent<CanvasScaler>(); scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution=new Vector2(1600,1000); scale.matchWidthOrHeight=.5f;
        var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(root.transform,false);
        var rect=panel.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
        panel.GetComponent<Image>().color=new Color(.06f,.09f,.13f);
        if(scene=="main menu")
        {
            Label(panel.transform,"弾幕 × アクアリウム",40,30,1520,70,36);
            Label(panel.transform,"ステージを選んでノベルへ進みます。設定は観察開始時に反映されます。",40,105,1520,50,24);
            for(int i=0;i<9;i++)
            {
                int stage=i; bool unlocked=CampaignStore.Data.StageUnlocked(i);
                int completed=CampaignStore.Data.completed.FindAll(id=>id.StartsWith(stage+"-")).Count;
                Button(panel.transform,$"ステージ {i}  {(unlocked?completed+"目標達成":"未解放")}",40+(i%3)*510,185+(i/3)*125,480,100,
                    ()=>{CampaignRuntime.SelectedStage=stage;SceneManager.LoadScene("nobel0");},unlocked);
            }
            Button(panel.transform,"生態系の設定",40,600,480,80,()=>SceneManager.LoadScene("setting"));
            if(CampaignStore.Data.StageUnlocked(9))
            {
                Button(panel.transform,"ステージ9のノベル入口",550,600,480,80,()=>{CampaignRuntime.SelectedStage=9;SceneManager.LoadScene("nobel0");});
                Label(panel.transform,"ステージ9を解放済み（戦闘・ノベルは今後の制作対象）",40,720,1520,70,24);
            }
        }
        else if(scene=="debriefing0")
        {
            Label(panel.transform,"観察結果",40,20,1520,60,36);
            Label(panel.transform,"達成目標・解放",40,95,460,40,24);
            ScrollText(panel.transform,"Result Summary",CampaignRuntime.LastSummary??"今回の観察結果はありません。",40,145,460,635,22);
            Label(panel.transform,"生態系ログ（生データ）",530,95,1030,40,24);
            ScrollText(panel.transform,"Ecosystem Raw Log",string.IsNullOrEmpty(CampaignRuntime.LastEcosystemLog)
                ? "今回の生態系ログはありません。" : CampaignRuntime.LastEcosystemLog,530,145,1030,635,20);
            Button(panel.transform,"再挑戦",40,820,440,75,()=>StartBattle());
            Button(panel.transform,"メニュー",540,820,440,75,()=>SceneManager.LoadScene("main menu"));
            int next=CampaignRuntime.SelectedStage+1;
            Button(panel.transform,"次のノベルへ",1040,820,500,75,()=>{CampaignRuntime.SelectedStage=next;SceneManager.LoadScene("nobel0");},next<=9 && CampaignStore.Data.StageUnlocked(next));
        }
        else
        {
            int stage=CampaignRuntime.SelectedStage;
            Label(panel.transform,"ステージ "+stage+" — ノベルパート",40,40,1520,70,36);
            string story=novelTexts!=null && stage>=0 && stage<novelTexts.Length && novelTexts[stage]!=null
                ? novelTexts[stage].text : "このステージのノベル本文は未登録です。";
            ScrollText(panel.transform,"Novel Text",story,40,140,1520,650,28);
            Button(panel.transform,"観察を開始",40,820,700,85,StartBattle,stage<9 && CampaignStore.Data.StageUnlocked(stage));
            Button(panel.transform,"メニュー",820,820,700,85,()=>SceneManager.LoadScene("main menu"));
        }
        if(CampaignStore.SaveError!=null) Label(panel.transform,CampaignStore.SaveError,40,930,1520,60,20);
    }
    private void ScrollText(Transform parent,string name,string value,float x,float y,float width,float height,int fontSize)
    {
        var root=Rect(parent,name,x,y,width,height);
        root.gameObject.AddComponent<Image>().color=new Color(.08f,.12f,.17f);
        var scroll=root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal=false; scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45;
        var viewport=Rect(root,"Viewport",0,0,width-24,height);
        viewport.gameObject.AddComponent<Image>().color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect(viewport,"Content",0,0,width-24,0);
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;
        content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding=new RectOffset(12,12,10,10);layout.spacing=0;
        layout.childControlWidth=layout.childControlHeight=true;
        layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        // Bound each Text mesh so long observations do not hit uGUI's vertex limit.
        // Prefer existing line boundaries and retain every character of the raw log.
        for(int offset=0;offset<value.Length;)
        {
            int length=Mathf.Min(4000,value.Length-offset);
            if(offset+length<value.Length)
            {
                int newline=value.LastIndexOf('\n',offset+length-1,length);
                if(newline>=offset)length=newline-offset+1;
            }
            var text=Label(content,value.Substring(offset,length),0,0,width-48,0,fontSize);
            text.supportRichText=false;text.alignment=TextAnchor.UpperLeft;
            offset+=length;
        }
        var track=Rect(root,"Vertical Scrollbar",width-18,0,18,height);
        track.gameObject.AddComponent<Image>().color=new Color(.14f,.21f,.27f);
        var handle=Rect(track,"Handle",0,0,18,height);
        handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;
        handle.offsetMin=handle.offsetMax=Vector2.zero;
        var graphic=handle.gameObject.AddComponent<Image>();graphic.color=new Color(.25f,.78f,.72f);
        var scrollbar=track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect=handle;scrollbar.targetGraphic=graphic;
        scrollbar.direction=Scrollbar.Direction.BottomToTop;
        scroll.viewport=viewport;scroll.content=content;scroll.verticalScrollbar=scrollbar;
        Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;
    }
    private static void StartBattle()
    {
        int stage=CampaignRuntime.SelectedStage;
        if(stage>=0 && stage<9 && CampaignStore.Data.StageUnlocked(stage)) SceneManager.LoadScene("battle"+stage);
    }
    private RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
    {
        var obj=new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false);
        var r=obj.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
    }
    private Text Label(Transform parent,string text,float x,float y,float width,float height,int size)
    {
        var label=Rect(parent,"Label",x,y,width,height).gameObject.AddComponent<Text>();
        label.font=japaneseFont!=null?japaneseFont:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text=text;label.fontSize=size;label.color=Color.white;label.raycastTarget=false;
        label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;return label;
    }
    private void Button(Transform parent,string caption,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool enabled=true)
    {
        var r=Rect(parent,caption,x,y,w,h); var image=r.gameObject.AddComponent<Image>();image.color=new Color(.15f,.27f,.32f);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.interactable=enabled;button.onClick.AddListener(action);
        var label=Label(r,caption,10,10,w-20,h-20,26);label.alignment=TextAnchor.MiddleCenter;
        if(!enabled)label.color=Color.gray;
    }
}
