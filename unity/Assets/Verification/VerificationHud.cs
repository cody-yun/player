// Sample-only UGUI presentation. Source-mapped controllers remain independent of UGUI.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VerificationHud : MonoBehaviour
{
    private static readonly Color Background=Hex(0x11191F),Stage=Hex(0x19242D),Surface=Hex(0x212F39),
        Line=Hex(0x374955),TextColor=Hex(0xF0F5F7),Muted=Hex(0xB1C0CA),Accent=Hex(0xB8E7D5),Ink=Hex(0x142C23),Warning=Hex(0xFFC9AF);
    [SerializeField] private Font font;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasScaler scaler;
    [SerializeField] private RectTransform safe,header,stage,panel,content,cameraSection,micSection,poseSection,avatarBounds;
    [SerializeField] private RawImage avatar;
    [SerializeField] private LivLiv.Unity.UI.PosterPresenter poster;
    [SerializeField] private Text title,session,model,stageHint,cameraState,cameraHint,micState,micHint,poseHint;
    [SerializeField] private Button cameraButton,micButton,calibrateButton;
    [SerializeField] private Text cameraLabel,micLabel;
    [SerializeField] private Button[] poses;
    [SerializeField] private Image level;
    private PortVerification host;private Rect lastSafe;private Vector2 lastScreen;private float lastScale;
    private static readonly string[] PoseNames={"neutral","turn","talk"};
    private string selectedPose="neutral";private double calibratedUntil;
    public Font MarkFont=>font;
    public bool Compact {get;private set;}
    public int ButtonCount=>6;
    [Serializable]public sealed class LayoutEvidence
    {
        public int width,height,visibleButtons,clippedButtons,overlappingPairs,scrollComponents;
        // Android input coordinates have their origin at the screen's top-left.
        public int cameraX,cameraY,microphoneX,microphoneY;
        public string orientation;public bool compact;
    }
    // QA geometry in actual screen pixels; does not force layout during a render frame.
    public LayoutEvidence ReadLayoutEvidence()
    {
        var report=new LayoutEvidence{width=Screen.width,height=Screen.height,orientation=Screen.orientation.ToString(),compact=Compact};
        var controls=new[]{cameraButton,calibrateButton,micButton,poses[0],poses[1],poses[2]};
        var bounds=new Rect[controls.Length];var corners=new Vector3[4];var safeArea=Screen.safeArea;
        for(int i=0;i<controls.Length;i++)
        {
            var button=controls[i];if(button==null||!button.gameObject.activeInHierarchy)continue;
            report.visibleButtons++;((RectTransform)button.transform).GetWorldCorners(corners);
            var min=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            var rect=Rect.MinMaxRect(min.x,min.y,max.x,max.y);bounds[i]=rect;
            if(i==0){report.cameraX=Mathf.RoundToInt(rect.center.x);report.cameraY=Screen.height-Mathf.RoundToInt(rect.center.y);}
            if(i==2){report.microphoneX=Mathf.RoundToInt(rect.center.x);report.microphoneY=Screen.height-Mathf.RoundToInt(rect.center.y);}
            if(rect.width<=0||rect.height<=0||rect.xMin<safeArea.xMin-1||rect.yMin<safeArea.yMin-1||rect.xMax>safeArea.xMax+1||rect.yMax>safeArea.yMax+1)report.clippedButtons++;
            for(int j=0;j<i;j++)if(rect.Overlaps(bounds[j]))report.overlappingPairs++;
        }
        report.scrollComponents=GetComponentsInChildren<ScrollRect>(true).Length+GetComponentsInChildren<Scrollbar>(true).Length;
        return report;
    }
    public LivLiv.Unity.UI.PosterPresenter Poster=>poster;
    public void BindPoster(LivLiv.PlayerBase source)=>poster.Bind(source);
    private static Color Hex(uint rgb)=>new Color((rgb>>16&255)/255f,(rgb>>8&255)/255f,(rgb&255)/255f,1);

    // Called by the CLI scene builder; the resulting Canvas and controls are serialized, editable GameObjects.
    public void Build(Font uiFont,Material avatarMaterial)
    {
        font=uiFont;if(font==null)throw new ArgumentNullException(nameof(uiFont));
        var root=Node("StudioCanvas",transform);canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
        root.gameObject.AddComponent<GraphicRaycaster>();Fill(root,Background);
        safe=Node("SafeArea",root);Stretch(safe);
        header=Node("Header",safe);title=Label("Title",header,"LivLiv  /  캐릭터 스튜디오",22,TextColor);Place(title.rectTransform,20,0,450,64);
        session=Label("Session",header,"입력 대기",14,Muted);session.alignment=TextAnchor.MiddleRight;
        stage=Node("CharacterStage",safe);Fill(stage,Stage);
        var stageRule=Node("StageRule",stage);Fill(stageRule,Line);stageRule.anchorMin=new Vector2(0,1);stageRule.anchorMax=Vector2.one;stageRule.pivot=new Vector2(0,1);stageRule.sizeDelta=new Vector2(0,1);
        avatarBounds=Node("AvatarBounds",stage);Stretch(avatarBounds,20,38,20,54);
        var cover=Node("LoadingPoster",avatarBounds);Stretch(cover);
        var coverImage=cover.gameObject.AddComponent<RawImage>();coverImage.raycastTarget=false;coverImage.enabled=false;
        poster=cover.gameObject.AddComponent<LivLiv.Unity.UI.PosterPresenter>();
        var image=Node("Character",avatarBounds);Stretch(image);avatar=image.gameObject.AddComponent<RawImage>();avatar.raycastTarget=false;avatar.material=avatarMaterial;
        var fit=image.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=2f/3;
        model=Label("CharacterName",stage,"Aoi",22,TextColor);model.fontStyle=FontStyle.Bold;
        stageHint=Label("StageHint",stage,"카메라를 시작해 표정을 연결하세요",14,Muted);
        panel=Node("Controls",safe);Fill(panel,Surface);
        content=Node("Content",panel);Stretch(content,16,12,16,12);
        cameraSection=Node("CameraSection",content);micSection=Node("MicrophoneSection",content);poseSection=Node("PoseSection",content);
        var cameraTitle=Label("CameraTitle",cameraSection,"카메라",18,TextColor);PlaceWide(cameraTitle.rectTransform,0,0,100,24);
        cameraState=Label("CameraState",cameraSection,"꺼짐",14,Muted);cameraState.alignment=TextAnchor.MiddleRight;PlaceWide(cameraState.rectTransform,100,0,0,24);
        cameraButton=ActionButton("CameraButton",cameraSection,"카메라 시작",true,out cameraLabel);var cameraRect=(RectTransform)cameraButton.transform;
        cameraRect.anchorMin=new Vector2(0,1);cameraRect.anchorMax=new Vector2(.6f,1);cameraRect.offsetMin=new Vector2(0,-76);cameraRect.offsetMax=new Vector2(-4,-28);
        calibrateButton=ActionButton("CalibrateButton",cameraSection,"정면 맞추기",false,out _);var calibrateRect=(RectTransform)calibrateButton.transform;
        calibrateRect.anchorMin=new Vector2(.6f,1);calibrateRect.anchorMax=Vector2.one;calibrateRect.offsetMin=new Vector2(4,-76);calibrateRect.offsetMax=new Vector2(0,-28);calibrateButton.GetComponentInChildren<Text>().fontSize=14;
        cameraHint=Label("CameraHint",cameraSection,"고개와 표정을 따라 움직입니다.",13,Muted);PlaceWide(cameraHint.rectTransform,0,80,0,30);
        var micTitle=Label("MicrophoneTitle",micSection,"마이크",18,TextColor);PlaceWide(micTitle.rectTransform,0,0,100,24);
        micState=Label("MicrophoneState",micSection,"꺼짐",14,Muted);micState.alignment=TextAnchor.MiddleRight;PlaceWide(micState.rectTransform,100,0,0,24);
        micButton=ActionButton("MicrophoneButton",micSection,"마이크 시작",false,out micLabel);PlaceWide((RectTransform)micButton.transform,0,28,0,48);
        micHint=Label("MicrophoneHint",micSection,"목소리에 맞춰 입 모양을 만듭니다.",13,Muted);PlaceWide(micHint.rectTransform,0,80,0,30);
        var levelBack=Node("MouthActivity",micSection);Fill(levelBack,Line);PlaceWide(levelBack,0,76,0,2);level=Fill(Node("Level",levelBack),Accent);Stretch(level.rectTransform);level.rectTransform.anchorMax=new Vector2(0,1);
        var poseTitle=Label("PoseTitle",poseSection,"표정 미리보기",18,TextColor);PlaceWide(poseTitle.rectTransform,0,0,0,24);
        poses=new Button[3];string[] names={"기본","고개","말하기"};
        for(int i=0;i<3;i++)
        {
            poses[i]=ActionButton("Pose"+i,poseSection,names[i],false,out _);var r=(RectTransform)poses[i].transform;
            r.anchorMin=new Vector2(i/3f,1);r.anchorMax=new Vector2((i+1)/3f,1);r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(i==0?0:4,-76);r.offsetMax=new Vector2(i==2?0:-4,-28);
        }
        poseHint=Label("PoseHint",poseSection,"입력을 끈 상태에서 확인할 수 있어요.",13,Muted);PlaceWide(poseHint.rectTransform,0,80,0,30);
        if(FindFirstObjectByType<EventSystem>()==null){var events=new GameObject("UIEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));events.transform.SetParent(transform,false);}
    }
    public void Bind(PortVerification controller,RenderTexture texture,LivLiv.PlayerBase player)
    {
        host=controller;avatar.texture=texture;
        poster.Bind(player);
        cameraButton.onClick.AddListener(()=>host.ToggleCamera());micButton.onClick.AddListener(()=>host.ToggleMicrophone());
        calibrateButton.onClick.AddListener(()=>{host.CalibrateFace();calibratedUntil=Time.unscaledTimeAsDouble+2;});
        string[] names={"neutral","turn","talk"};for(int i=0;i<poses.Length;i++){string name=names[i];poses[i].onClick.AddListener(()=>{selectedPose=name;host.SetPose(name);});}
        EventSystem.current?.SetSelectedGameObject(cameraButton.gameObject);Layout();
    }
    private void LateUpdate()
    {
        if(host==null)return;Layout();var s=host.ReadHudState();
        model.text=s.model??"캐릭터 불러오는 중";
        cameraState.text=s.cameraStarting?"준비 중":s.cameraActive?(s.faceSeen?"추적 중":"얼굴 찾는 중"):s.cameraError!=null?"연결 실패":"꺼짐";
        cameraState.color=s.cameraError!=null?Warning:s.faceSeen?Accent:Muted;
        micState.text=s.micStarting?"준비 중":s.micActive?"사용 중":s.micError!=null?"연결 실패":"꺼짐";micState.color=s.micError!=null?Warning:s.micActive?Accent:Muted;
        cameraLabel.text=s.cameraStarting?"시작 취소":s.cameraActive?"카메라 중지":"카메라 시작";
        micLabel.text=s.micStarting?"시작 취소":s.micActive?"마이크 중지":"마이크 시작";
        cameraHint.text=s.cameraError??(s.cameraStarting?"권한을 허용하면 추적을 시작합니다.":s.cameraActive?(s.faceSeen?"고개와 표정이 연결됐어요.":"얼굴이 카메라에 보이도록 맞춰 주세요."):"고개와 표정을 따라 움직입니다.");
        micHint.text=s.micError??(s.micStarting?"권한을 허용하면 입력을 시작합니다.":s.micActive?"마이크에 말해 보세요.":"목소리에 맞춰 입 모양을 만듭니다.");
        cameraHint.color=s.cameraError!=null?Warning:Muted;micHint.color=s.micError!=null?Warning:Muted;
        cameraButton.interactable=micButton.interactable=s.ready;
        calibrateButton.interactable=s.ready&&s.cameraActive&&s.faceSeen;
        calibrateButton.GetComponentInChildren<Text>().text=Time.unscaledTimeAsDouble<calibratedUntil?"설정 완료":"정면 맞추기";
        calibrateButton.GetComponentInChildren<Text>().color=calibrateButton.interactable?TextColor:Muted;
        bool live=s.cameraActive||s.micActive||s.cameraStarting||s.micStarting;
        if(live)selectedPose="neutral";
        for(int i=0;i<poses.Length;i++){poses[i].interactable=s.ready&&!live;var img=poses[i].targetGraphic;img.color=!live&&selectedPose==PoseNames[i]?Line:Surface;}
        poseHint.text=live?"입력을 중지하면 미리볼 수 있어요.":"기본 자세와 움직임을 확인해 보세요.";
        session.text=!s.ready?"준비 중":live?"라이브 연결":"입력 대기";session.color=live?Accent:Muted;
        stageHint.text=s.fatal?"캐릭터를 불러오지 못했어요. 앱을 다시 열어 주세요.":s.cameraActive?(s.faceSeen?"표정을 지으면 캐릭터가 따라 해요":"카메라에서 얼굴을 찾고 있어요"):s.micActive?"목소리에 맞춰 캐릭터가 말해요":"카메라를 시작해 표정을 연결하세요";
        level.rectTransform.anchorMax=new Vector2(s.micActive?Mathf.Clamp01((float)s.mouth):0,1);
    }
    private void Layout()
    {
        var screen=new Vector2(Screen.width,Screen.height);var area=Screen.safeArea;
        float density=Application.isMobilePlatform?Mathf.Clamp(Screen.dpi>0?Screen.dpi/160f:Mathf.Min(screen.x,screen.y)/540f,1,3):Mathf.Max(1,Mathf.Min(screen.x/1280f,screen.y/800f));
        // Small windows retain every control. Cap UI density after choosing a portrait/landscape composition.
        density=Mathf.Min(density,area.width/320f,area.height/(area.width<area.height?480f:400f));
        if(screen==lastScreen&&area==lastSafe&&Mathf.Approximately(density,lastScale))return;
        lastScreen=screen;lastSafe=area;lastScale=density;scaler.scaleFactor=density;
        safe.anchorMin=new Vector2(area.xMin/screen.x,area.yMin/screen.y);safe.anchorMax=new Vector2(area.xMax/screen.x,area.yMax/screen.y);safe.offsetMin=safe.offsetMax=Vector2.zero;
        float w=area.width/density,h=area.height/density;Compact=w<760&&w<h;
        float headerHeight=Compact||h<600?44:64;Place(header,0,0,w,headerHeight);
        title.fontSize=Compact||h<600?18:22;Place(title.rectTransform,Compact?16:24,0,w-120,headerHeight);Place(session.rectTransform,w-108,0,92,headerHeight);
        float panelHeight,panelWidth;
        if(Compact)
        {
            panelHeight=h>=600?380:340;panelWidth=w;
            Place(stage,12,headerHeight,w-24,h-headerHeight-panelHeight-20);Place(panel,0,h-panelHeight,w,panelHeight);
        }
        else
        {
            panelWidth=Mathf.Clamp(w*.34f,292,352);panelHeight=h-headerHeight-12;
            Place(stage,12,headerHeight,w-panelWidth-36,panelHeight);Place(panel,w-panelWidth-12,headerHeight,panelWidth,panelHeight);
        }
        float innerHeight=panelHeight-24,innerWidth=panelWidth-32;
        bool showPoseHint=innerHeight>=346;poseHint.gameObject.SetActive(showPoseHint);
        float poseHeight=showPoseHint?110:76,gap=Mathf.Clamp((innerHeight-220-poseHeight)/2,8,28);
        Place(cameraSection,0,0,innerWidth,110);Place(micSection,0,110+gap,innerWidth,110);Place(poseSection,0,220+gap*2,innerWidth,poseHeight);
        model.rectTransform.anchorMin=model.rectTransform.anchorMax=Vector2.zero;model.rectTransform.pivot=Vector2.zero;model.rectTransform.anchoredPosition=new Vector2(20,29);model.rectTransform.sizeDelta=new Vector2(240,28);
        stageHint.rectTransform.anchorMin=Vector2.zero;stageHint.rectTransform.anchorMax=new Vector2(1,0);stageHint.rectTransform.pivot=Vector2.zero;stageHint.rectTransform.offsetMin=new Vector2(20,8);stageHint.rectTransform.offsetMax=new Vector2(-20,28);
        stageHint.fontSize=Compact?12:14;
        bool smallStage=Compact&&h<600;stageHint.gameObject.SetActive(!smallStage);model.fontSize=smallStage?16:22;
        if(smallStage){model.rectTransform.anchoredPosition=new Vector2(12,4);Stretch(avatarBounds,8,4,8,28);}
        else Stretch(avatarBounds,20,12,20,64);
    }
    private static RectTransform Node(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
    private static Image Fill(RectTransform r,Color color){var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
    private Text Label(string name,Transform parent,string value,int size,Color color)
    {var t=Node(name,parent).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
    private Button ActionButton(string name,Transform parent,string value,bool primary,out Text label)
    {
        var r=Node(name,parent);var image=Fill(r,primary?Accent:Line);image.raycastTarget=true;var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.9f,1,.96f);colors.pressedColor=new Color(.7f,.85f,.8f);colors.selectedColor=new Color(.8f,1,.92f);colors.disabledColor=new Color(.58f,.63f,.65f,.65f);colors.fadeDuration=.12f;button.colors=colors;
        var focus=r.gameObject.AddComponent<HudFocus>();focus.outline=r.gameObject.AddComponent<Outline>();focus.outline.effectColor=Accent;focus.outline.effectDistance=new Vector2(2,-2);focus.outline.enabled=false;
        label=Label("Label",r,value,16,primary?Ink:TextColor);label.alignment=TextAnchor.MiddleCenter;Stretch(label.rectTransform,8,0,8,0);return button;
    }
    private static void Stretch(RectTransform r,float l=0,float t=0,float right=0,float b=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-right,-t);}
    private static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static void PlaceWide(RectTransform r,float l,float y,float right,float h){r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(0,1);r.offsetMin=new Vector2(l,-y-h);r.offsetMax=new Vector2(-right,-y);}
}
