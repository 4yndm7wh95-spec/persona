using System;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>高清透明门素材；门板按左铰链作三维旋转，再映射回同一相机。</summary>
internal sealed class GateView
{
	private const float Scale = 700f / 1577f;
	private static readonly Vector2 DesignSize = new(936f,1680f);
	private static readonly Vector2 Origin = new(470f, 1617f);
	private static readonly Vector2[] LeafCorners = { new(345,451),new(699,487),new(700,1519),new(342,1572) };
	private static readonly Vector2[] Canvas = {Vector2.Zero,new(936,0),DesignSize,new(0,1680)};
	private static readonly Vector2[][] FrameParts = {
		new[] {Vector2.Zero,new Vector2(936,0),new(936,487),LeafCorners[1],LeafCorners[0],new(0,451)},
		new[] {new Vector2(0,451),LeafCorners[0],LeafCorners[3],new(0,1572)},
		new[] {LeafCorners[1],new Vector2(936,487),new(936,1680),LeafCorners[2]},
		new[] {new Vector2(0,1572),LeafCorners[3],LeafCorners[2],new(936,1680),new(0,1680)},
	};
	private static readonly double[] CameraMap = Homography(
		new[] { Raw(new(-118,-587),0), Raw(new(118,-587),0), Raw(new(118,-18),0), Raw(new(-118,-18),0) }, LeafCorners);
	private static Shader? _shader;
	private readonly Node2D _model;
	private readonly Node2D _portal;
	private readonly Polygon2D[] _shaft = new Polygon2D[6];
	private readonly Polygon2D _closed, _leaf, _grille, _flash;
	private readonly Node2D _frame;
	private readonly Polygon2D[] _edges = new Polygon2D[4];
	private readonly ShaderMaterial _leafMaterial;
	private readonly Line2D _needle;
	private readonly bool _frameOnly;
	private float _open = -1f;
	public Node2D Content { get; }

	public GateView(Node2D root, bool frameOnly = false)
	{
		_frameOnly = frameOnly;
		// 原动画节点保留为 Tween 的存活 owner；可见节点使用认可素材。
		Content = new Node2D { Visible = false };
		root.AddChild(Content);
		Polygon2D ground = new() {
			Polygon = new[] { new Vector2(-900,-2400), new Vector2(900,-2400), new Vector2(900,0), new Vector2(-900,0) },
			ClipChildren = CanvasItem.ClipChildrenMode.Only,
		};
		root.AddChild(ground);
		_model = new Node2D { Position = -Origin * Scale, Scale = Vector2.One * Scale };
		ground.AddChild(_model);
		Texture2D approved = ResourceLoader.Load<Texture2D>("res://p3makoto/fx/door_approved_hd.png");
		Texture2D grille = ResourceLoader.Load<Texture2D>("res://p3makoto/fx/door_grille.png");
		_portal = new Node2D(); _model.AddChild(_portal);
		Polygon2D dark = new() { Polygon=LeafCorners, Color=new Color(.02f,.05f,.20f) }; _portal.AddChild(dark);
		for(int i=0;i<_shaft.Length;i++) { _shaft[i]=new Polygon2D { Color=new Color(.40f,.68f,.85f,.45f), Material=Fx.Additive }; _portal.AddChild(_shaft[i]); }
		_grille = Quad(_portal, grille, LeafCorners, new[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, out _);
		_grille.Modulate = new Color(.65f,.70f,.85f);
		for (int i=0;i<4;i++) { _edges[i]=new Polygon2D { Color=new Color("4b5666") }; _model.AddChild(_edges[i]); }
		Vector2[] uv = Array.ConvertAll(LeafCorners, p => p / DesignSize);
		_leaf = Quad(_model, approved, LeafCorners, uv, out _leafMaterial);
		_frame = new Node2D(); _model.AddChild(_frame);
		foreach(Vector2[] part in FrameParts) Pixels(_frame,approved,part);
		// 高清独立素材自带透明轮廓，无需再从截图用粗多边形切边。
		_closed = Pixels(_model, approved, Canvas);
		_needle = new Line2D { Width=3f, DefaultColor=new Color(.90f,.91f,.94f), Visible=false };
		_model.AddChild(_needle);
		_flash = Pixels(_model, approved, Canvas); _flash.Material = Fx.Additive; _flash.Modulate = new Color(1,1,1,0);
		SetOpen(0f, 0f);
	}

	private static Polygon2D Pixels(Node parent, Texture2D texture, Vector2[] points)
	{
		Polygon2D p = new() { Texture=texture, Polygon=points,
			UV=Array.ConvertAll(points,p=>p/DesignSize*texture.GetSize()) };
		parent.AddChild(p); return p;
	}

	private static Polygon2D Quad(Node parent, Texture2D texture, Vector2[] points, Vector2[] uv, out ShaderMaterial material)
	{
		_shader ??= new Shader { Code="""
shader_type canvas_item;
render_mode unshaded;
uniform vec3 h_u;
uniform vec3 h_v;
uniform vec3 h_w;
uniform bool backside = false;
varying vec2 point;
varying vec4 tint;
void vertex() { point = VERTEX; tint = COLOR; }
void fragment() {
	vec3 p = vec3(point,1.0);
	vec2 uv = vec2(dot(h_u,p),dot(h_v,p)) / dot(h_w,p);
	vec4 c = texture(TEXTURE, uv);
	if (backside) { c.rgb = vec3(.045,.065,.13); }
	COLOR = c * tint;
}
""" };
		material = new ShaderMaterial { Shader=_shader };
		Polygon2D quad = new() { Texture=texture, Polygon=points, UV=Array.ConvertAll(uv,p=>p*texture.GetSize()), Material=material };
		parent.AddChild(quad); SetMap(material,points,uv); return quad;
	}

	private static void SetMap(ShaderMaterial material, Vector2[] points, Vector2[] uv)
	{
		double[] h = Homography(points,uv);
		material.SetShaderParameter("h_u",new Vector3((float)h[0],(float)h[1],(float)h[2]));
		material.SetShaderParameter("h_v",new Vector3((float)h[3],(float)h[4],(float)h[5]));
		material.SetShaderParameter("h_w",new Vector3((float)h[6],(float)h[7],1f));
	}

	private static Vector2 Raw(Vector2 p,float z)
	{
		const float a=.82030475f, e=.12217305f, f=1400f;
		float x=Mathf.Cos(a)*p.X-Mathf.Sin(a)*z;
		float d=Mathf.Sin(a)*p.X+Mathf.Cos(a)*z;
		float y=p.Y+384f;
		float yy=Mathf.Cos(e)*y-Mathf.Sin(e)*d;
		float zz=Mathf.Sin(e)*y+Mathf.Cos(e)*d;
		return new Vector2(x,yy)*(f/(f+zz));
	}

	private static Vector2 Map(double[] h,Vector2 p)
	{
		double w=h[6]*p.X+h[7]*p.Y+1;
		return new Vector2((float)((h[0]*p.X+h[1]*p.Y+h[2])/w),(float)((h[3]*p.X+h[4]*p.Y+h[5])/w));
	}

	public static Vector2 Project(Vector2 p,float z=0) => (Map(CameraMap,Raw(p,z))-Origin)*Scale;

	public void SetOpen(float open,float needle)
	{
		_needle.Visible=needle>.01f && !_frameOnly;
		Vector2 dial=new(530,418);
		_needle.Points=new[] {dial,dial+Vector2.Up.Rotated(Mathf.DegToRad(-74+148*needle))*92f};
		if (Mathf.IsEqualApprox(open,_open)) return;
		_open=open;
		_closed.Visible=open<.001f && !_frameOnly;
		_frame.Visible=!_closed.Visible;
		_grille.Visible=open>=.001f && !_frameOnly;
		_portal.Visible=_grille.Visible;
		_leaf.Visible=open>=.001f && !_frameOnly;
		float angle=open*Mathf.DegToRad(76f);
		Vector2[] front=new Vector2[4],back=new Vector2[4];
		for(int i=0;i<4;i++)
		{
			float u=i==1||i==2?236f:0f, y=i>=2?-18f:-587f;
			float x=-118f+u*Mathf.Cos(angle), z=u*Mathf.Sin(angle);
			front[i]=Map(CameraMap,Raw(new Vector2(x,y),z));
			back[i]=Map(CameraMap,Raw(new Vector2(x,y),z+10f));
		}
		for(int i=0;i<4;i++)
		{
			int j=(i+1)%4;
			_edges[i].Visible=_leaf.Visible;
			_edges[i].Polygon=new[] {front[i],front[j],back[j],back[i]};
		}
		// 极窄的铰链掠过相机时隐藏一瞬，避免求解退化四边形。
		if (Mathf.Abs(Mathf.Cos(.82030475f+angle))<.025f) { _leaf.Visible=false; return; }
		_leaf.Polygon=front;
		Vector2[] uv=Array.ConvertAll(LeafCorners,p=>p/DesignSize);
		SetMap(_leafMaterial,front,uv);
		_leafMaterial.SetShaderParameter("backside",Mathf.Cos(.82030475f+angle)<0f);
	}

	public void Rise(float y) => _model.Position=-Origin*Scale+new Vector2(0,y);
	public void Tint(Color tint) => _model.Modulate=tint;
	public void Flash(Color color) => _flash.Modulate=color;
	public void Tick(float time)
	{
		double[] map=Homography(new[] {Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},LeafCorners);
		for(int i=0;i<_shaft.Length;i++)
		{
			float y=(time*1.1f+i*.167f)%1.12f-.06f;
			float a=Mathf.Clamp(y,0,1),b=Mathf.Clamp(y+.025f+(i%3)*.015f,0,1);
			_shaft[i].Visible=b>a;
			if(b>a) _shaft[i].Polygon=new[] {Map(map,new(0,a)),Map(map,new(1,a)),Map(map,new(1,b)),Map(map,new(0,b))};
		}
	}

	private static double[] Homography(Vector2[] src,Vector2[] dst)
	{
		double[,] m=new double[8,9];
		for(int i=0;i<4;i++)
		{
			double x=src[i].X,y=src[i].Y,u=dst[i].X,v=dst[i].Y;int r=2*i;
			m[r,0]=x;m[r,1]=y;m[r,2]=1;m[r,6]=-u*x;m[r,7]=-u*y;m[r,8]=u;
			m[r+1,3]=x;m[r+1,4]=y;m[r+1,5]=1;m[r+1,6]=-v*x;m[r+1,7]=-v*y;m[r+1,8]=v;
		}
		for(int i=0;i<8;i++)
		{
			int pivot=i;for(int k=i+1;k<8;k++)if(Math.Abs(m[k,i])>Math.Abs(m[pivot,i]))pivot=k;
			for(int j=i;j<9;j++)(m[i,j],m[pivot,j])=(m[pivot,j],m[i,j]);
			double d=m[i,i];if(Math.Abs(d)<1e-12)throw new InvalidOperationException("Degenerate door projection");
			for(int j=i;j<9;j++)m[i,j]/=d;
			for(int k=0;k<8;k++)if(k!=i){double a=m[k,i];for(int j=i;j<9;j++)m[k,j]-=a*m[i,j];}
		}
		double[] h=new double[8];for(int i=0;i<8;i++)h[i]=m[i,8];return h;
	}
}
