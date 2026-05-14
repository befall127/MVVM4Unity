简介
        在wpf中，引入的一个比较新颖概念非xaml莫属，而xaml的，大概就是mvvm框架的最好的实践。虽然网络时代的兴起，wpf不会那么火热，了解的人不多。但这里面一些思想，如数据绑定，弱化界面层的逻辑等，被做程序的一群人发扬光大。用在了不同语言及环境下。在不久前还一直都只使用puremvc框架，当然很好的解决了不同程序模块的解耦合的问题，但一真有一块心病，那就是view层在厚。一是Monobehaiver本身就是继承了太多的属性和方法，再加上自定义了一些子控件和用户控件，必然会让面板的代码异常复杂，即便在其中调用独立的控制器脚本，也需要在对应的view层脚本中写大量的代码。再有就是当界面的逻辑是动态的时候，就要写判断，不便于扩展。为此，希望一些时候逻辑可以动态传入面板，而面板只用于显示信息及反馈一些事件。此时，才想到要将mvvm框架集成到项目中，在使用过之后，才发现，它和puremvc框架是不同层次的东西。如果你喜欢，可以一起使用。

一.绑定
    数据绑定
    当数据源发生变化时，可以直接在ui界面上表现出来。你在没有接触到数据绑定的情况下，可能会使用观察者模式。其实，这里所使用的类似观察者模式。区别是，这常常不是一对多的情况,虽然有，但是一般还是一对一比较多。

    在进行数据模型发生变化的时候，得考虑如何触发事件。第一种方法，就是在对数据进行赋值的时候，中间加一级，那就是对属性赋值，当属性改变的同时，触发目标事件；第二种方法，就是封装一个数据模块类，它其中可以保存指定格式的数据，当对它的数据赋值的时候，就可以触发相应的事件。

    由于使用泛型，在不太好记录到字典中，所以增加一了个接口。下面是这个类的具体代码：

```c#
public interface IBindableProperty
    {
        object ValueBoxed { get; set; }
    }

    public class BindableProperty<T> : IBindableProperty
    {
        public event UnityAction<T> onValueChanged = delegate { };

        private T _value = default(T);
        public T Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    ValueChanged(_value);
                }
            }
        }

        public object ValueBoxed
        {
            get { return Value; }
            set { Value = (T)value; }
        }
        private void ValueChanged(T value)
        {
            if (onValueChanged != null)
                onValueChanged.Invoke(value);
        }
        public void RegistValueChanged(UnityAction<T> OnValueChanged)
        {
            this.onValueChanged += OnValueChanged;
        }

        public void RemoveValueChanged(UnityAction<T> OnValueChanged)
        {
            this.onValueChanged -= OnValueChanged;
        }

        public override string ToString()
        {
            return (Value != null ? Value.ToString() : "null");
        }

        public void Clear()
        {
            _value = default(T);
        }


    }
```


 事件绑定
    界面上一般会触发一些事件，比如点击，滑动等。将这些事件触发传回viewmodel层，也是非常之关键。你自然想到的还是事件注册的方法。但这一次用到的是unity自己提供的UnityEventBase的继承类。因为它就相当于事件的容器。一但viewmodel注册后，将其中的方法对应到view层的事件容器中，当界面事件触发的时候就可以触发相应的事件了。

    这里也有两种实现方案。第一种就是利用反射，当界面触发后，在viewmodel中利用反射找到对应的方法，然后invoke它。这样也是可行的，但有一个问题，那在vs编辑器下就是可以直观的看到，这个方法将会没有引用。如果你的项目交到不熟悉的人手上，那么这个方法有可能被别人删除掉，这样不安全。另外一中方案，就是把事件当作一个object对象也用数据绑定的中的模板类来存放。只是需要在viewmodel中初始化的时候指定各个事件，而且传入的参数必须一统一规范。

    下面是事件绑定时，注册的两个方法体，一种是带参的，一种是无参的：

```C#
/// <summary>
        /// 注册通用事件
        /// </summary>
        /// <param name="uEvent"></param>
        /// <param name="sourceName"></param>
        public virtual void RegistEvent(UnityEvent uEvent, string sourceName,params object[] arguments)
        {
            UnityAction action = () =>
            {
                var prop = viewModel.GetBindableProperty<PanelEvent>(sourceName);
                if (prop.Value != null)
                {
                    var func = prop.Value;
                    func.Invoke(Context as PanelBase,arguments);
                }
            };

            binders += viewModel =>
            {
                uEvent.AddListener(action);
            };

            unbinders += viewModel =>
            {
                uEvent.RemoveListener(action);
            };
        }

        /// <summary>
        /// 注册事件
        /// (其中arguments中的参数只能是引用类型,否则无法正常使用)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="uEvent"></param>
        /// <param name="sourceName"></param>
        /// <param name="arguments"></param>
        internal virtual void RegistEvent<T>(UnityEvent<T> uEvent,string sourceName, params object[] arguments)
        {
            UnityAction<T> action = (x) =>
            {
                var prop = viewModel.GetBindableProperty<PanelEvent>(sourceName);
                if (prop.Value != null)
                {
                    var func = prop.Value;
                    func.Invoke(Context as PanelBase, arguments);
                }
            };

            binders += viewModel =>
            {
                uEvent.AddListener(action);
            };

            unbinders += viewModel =>
            {
                uEvent.RemoveListener(action);
            };
        }
```


二.代码生成
由于绑定的方法是固定的，如果这一部分每将都让程序员来自己写，必然会很是无趣。

   虽然unity2018已经加入对.net4.6的支持，但还是有很多unity5.3的项目存在，所以，我没有直接使用Nefactory的.net4.0版本。而是，将其修改为 支持.net3.5的版本，其中可能会有一些问题，但至少c#代码读取与生成这部分没有问题了。

   思路也比较简单，就是自动的将你定义好的控件，及需要绑定的名称进行关联。其中的难点在于，如果你写好了脚本，这些信息也可以反射解析到配制器上面。

   由于篇幅有限，这时就只给出代码生成入口和解析入口：

```C#
/// <summary>
        /// 创建代码
        /// </summary>
        /// <param name="go"></param>
        /// <param name="components"></param>
        /// <param name="rule"></param>
        public static void CreateScript(GameObject go, List<ComponentItem> components, GenCodeRule rule)
        {
            var uiCoder = GenCodeUtil.LoadUICoder(go, rule);

            var baseType = GenCodeUtil.supportBaseTypes[rule.baseTypeIndex];

            var needAdd = FilterExisField(baseType, components);

            var tree = uiCoder.tree;
            var className = uiCoder.className;
            var classNode = tree.Descendants.OfType<TypeDeclaration>().Where(x =>  == className).First();

            //创建控件字段
            CreateMemberFields(classNode, needAdd);
            //创建根方法
            CompleteBaseMethods(classNode, rule);
            //绑定擦伤信息
            BindingInfoMethods(classNode, needAdd);
            //对代码元素进行排序
            SortClassMembers(classNode);

            var scriptPath = AssetDatabase.GetAssetPath(go).Replace(".prefab", ".cs");

            System.IO.File.WriteAllText(scriptPath, uiCoder.Compile());
            var type = typeof(BridgeUI.PanelBase).Assembly.GetType(className);
            if (type != null)
            {
                if (go.GetComponent(type) == null)
                {
                    go.AddComponent(type);
                }
            }
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
            };
        }

        /// <summary>
        /// 分析代码的的组件信息
        /// </summary>
        /// <param name="component"></param>
        /// <param name="components"></param>
        public static void AnalysisComponent(PanelBase component, List<ComponentItem> components)
        {
            var fields = component.GetType().GetFields(BindingFlags.GetField | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(field.FieldType))
                {
                    var compItem = components.Find(x => "m_" +  ==  ||  == );

                    if (compItem == null)
                    {
                        compItem = new ComponentItem();
                        compItem.name = .Replace("m_", "");
                        components.Add(compItem);
                    }

                    var value = field.GetValue(component);
                    if (value != null)
                    {
                        if (field.FieldType == typeof(GameObject))
                        {
                            compItem.target = value as GameObject;
                        }
                        else
                        {
                            compItem.target = (value as MonoBehaviour).gameObject;
                        }

                        compItem.components = SortComponent(compItem.target);
                    }
                }
            }
            AnalysisBindings(component, components);
        }
```


三.与Lua结合
        现在热更新最火的怕是是xlua了，能和lua代码中的方法进行绑定交互，算是一种扩展吧。

        其实本原理并不是要在lua中来注册事件，仅仅是c#中注册了去调用一调。但是想到viewmodel层可以直接写到lua中，我当时还是有点小激动。

        要将由于面板中需要的viewmodel其实是c#写的，所以我们需要一个适配器。下面是这个lua专用viewmodel类：（由于不能直接确定这个viewmodel中是不是含有一个方法或属性，所以重写了GetBindableProperty,让lua模块自己去确定）

```C#
protected class LuaViewModel : Binding.ViewModelBase
        {
            protected LuaTable scriptEnv;

            public LuaViewModel(LuaTable scriptEnv)
            {
                this.scriptEnv = scriptEnv;
            }

            public override BindableProperty<T> GetBindableProperty<T>(string name)
            {
                var prop = base.GetBindableProperty<T>(name);
                if (prop.ValueBoxed == null)
                {
                    prop.Value = scriptEnv.Get<T>(name);
                }
                return prop;
            }
        }
```


有了上面这些功课，其实不用一个可视化配制界面，也是可以进行mvvm开发了。但代码生成都已经有了，为何不搞一个快速的实现指定控件+进行绑定+生成代码的可视化界面呢。

       先吧，这个界面给大家看一看吧，也好说明怎么用：

        注释：此处没有图片，内容较为简单，就是一个普通的控件及事件的UI可视化界面

        这个界面就集成了控件的列表，及代码生成与绑定等。其中可以自己指定需要绑定的元素及事件（继承于unityEventBase并只有一个或没有参数）

        面代码生成的核心代码，则集成了生成本地绑定与viewmodel的绑定等，同时也集成了代码解析的逻辑。下面给出这个类：（主要是调用NRefactory模块）

```C#
using System;
using System.Linq;
using System.Collections.Generic;
using ICSharpCode.NRefactory.CSharp;

namespace BridgeUI.CodeGen
{
    public class ComponentCoder
    {
        protected MethodDeclaration InitComponentsNode;
        protected MethodDeclaration PropBindingsNode;
        protected TypeDeclaration classNode;

        public void SetContext(TypeDeclaration classNode)
        {
            this.classNode = classNode;
            InitComponentsNode = classNode.Descendants.OfType<MethodDeclaration>().Where(x =>  == GenCodeUtil.initcomponentMethod).FirstOrDefault();
            PropBindingsNode = classNode.Descendants.OfType<MethodDeclaration>().Where(x =>  == GenCodeUtil.propbindingsMethod).FirstOrDefault();
        }

        /// <summary>
        /// Binding关联
        /// </summary>
        /// <returns></returns>
        public virtual void CompleteCode(ComponentItem component)
        {
            foreach (var item in component.viewItems)
            {
                BindingMemberInvocations(component.name, item);
            }

            foreach (var item in component.eventItems)
            {
                if (item.runtime)
                {
                    BindingEventInvocations(component.name, item);
                }
                else
                {
                    LocalEventInvocations(component.name, item);
                }
            }
        }

        /// <summary>
        /// 远端member关联
        /// </summary>
        protected virtual void BindingMemberInvocations(string name, BindingShow bindingInfo)
        {
            var invocations = PropBindingsNode.Body.Descendants.OfType<InvocationExpression>();
            var arg0_name = "m_" + name + "." + bindingInfo.bindingTarget;
            var arg0 = string.Format("\"{0}\"", arg0_name);
            var arg1 = string.Format("\"{0}\"",bindingInfo.bindingSource);
            var invocation = invocations.Where(
                x => x.Target.ToString().Contains("Binder") && 
                x.Arguments.Count > 0 &&
                x.Arguments.First().ToString() == arg0 &&
                x.Arguments.ToArray()[1].ToString() == arg1).FirstOrDefault();

            if (invocation == null)
            {
                var typeName = bindingInfo.bindingTargetType.typeName;
                var methodName = string.Format("RegistMember<{0}>", typeName);
                if (!string.IsNullOrEmpty(methodName))
                {
                    invocation = new InvocationExpression();
                    invocation.Target = new MemberReferenceExpression(new IdentifierExpression("Binder"), methodName, new AstType[0]);
                    invocation.Arguments.Add(new PrimitiveExpression(arg0_name));
                    invocation.Arguments.Add(new PrimitiveExpression(bindingInfo.bindingSource));
                    PropBindingsNode.Body.Add(invocation);
                }

            }
        }
        /// <summary>
        /// 远端关联事件
        /// </summary>
        /// <param name="name"></param>
        /// <param name="bindingInfo"></param>

        protected virtual void BindingEventInvocations(string name, BindingEvent bindingInfo)
        {
            var invocations = PropBindingsNode.Body.Descendants.OfType<InvocationExpression>();
            var arg0_name = "m_" + name + "." + bindingInfo.bindingTarget;
            var arg1_name = string.Format("\"{0}\"",bindingInfo.bindingSource);

            var invocation = invocations.Where(
                x => x.Target.ToString().Contains("Binder") &&
                x.Arguments.Count > 0 &&
                x.Arguments.First().ToString() == arg0_name &&
                x.Arguments.ToArray()[1].ToString() == arg1_name).FirstOrDefault();

            if (invocation == null)
            {
                var methodName = "RegistEvent";
                if (!string.IsNullOrEmpty(methodName))
                {
                    invocation = new InvocationExpression();
                    invocation.Target = new MemberReferenceExpression(new IdentifierExpression("Binder"), methodName, new AstType[0]);
                    invocation.Arguments.Add(new IdentifierExpression(arg0_name));
                    invocation.Arguments.Add(new PrimitiveExpression(bindingInfo.bindingSource));
                    PropBindingsNode.Body.Add(invocation);
                }

            }
        }

        /// <summary>
        /// 本地事件关联
        /// </summary>
        protected virtual void LocalEventInvocations(string name, BindingEvent bindingInfo)
        {
            var invocations = InitComponentsNode.Body.Descendants.OfType<InvocationExpression>();
            var targetName = "m_" + name;
            var invocation = invocations.Where(x =>
            x.Target.ToString().Contains(targetName) &&
            x.Arguments.FirstOrDefault().ToString() == bindingInfo.bindingSource).FirstOrDefault();

            var eventName = bindingInfo.bindingTarget;//如onClick
            if (invocation == null && !string.IsNullOrEmpty(eventName) && !string.IsNullOrEmpty(bindingInfo.bindingSource))
            {
                invocation = new InvocationExpression();
                invocation.Target = new MemberReferenceExpression(new MemberReferenceExpression(new IdentifierExpression("m_" + name), eventName, new AstType[0]), "AddListener", new AstType[0]);
                invocation.Arguments.Add(new IdentifierExpression(bindingInfo.bindingSource));
                InitComponentsNode.Body.Add(invocation);
                CompleteMethod(bindingInfo);
            }
        }

        /// <summary>
        /// 完善本地绑定的方法
        /// </summary>
        /// <param name="item"></param>
        protected void CompleteMethod(BindingEvent item)
        {
            var funcNode = classNode.Descendants.OfType<MethodDeclaration>().Where(x =>  == item.bindingSource).FirstOrDefault();
            if (funcNode == null)
            {
                var parameter = item.bindingTargetType.type.GetMethod("AddListener").GetParameters()[0];
                List<ParameterDeclaration> arguments = new List<ParameterDeclaration>();
                var parameters = parameter.ParameterType.GetGenericArguments();
                int count = 0;
                foreach (var para in parameters)
                {
                    ParameterDeclaration argument = new ParameterDeclaration(new ICSharpCode.NRefactory.CSharp.PrimitiveType(), "arg" + count++);
                    arguments.Add(argument);
                }

                {
                    funcNode = new MethodDeclaration();
                    funcNode.Name = item.bindingSource;
                    funcNode.Modifiers |= Modifiers.Protected;
                    funcNode.ReturnType = new ICSharpCode.NRefactory.CSharp.PrimitiveType("void");
                    funcNode.Parameters.AddRange(arguments);
                    funcNode.Body = new BlockStatement();
                    classNode.AddChild(funcNode, Roles.TypeMemberRole);
                }

            }
        }

        /// <summary>
        /// 分析代码中的绑定关系
        /// </summary>
        /// <param name="components"></param>
        internal void AnalysisBinding(List<ComponentItem> components)
        {
            if (classNode != null)
            {
                if (InitComponentsNode != null)
                {
                    var invctions = InitComponentsNode.Body.Descendants.OfType<InvocationExpression>();
                    foreach (var item in invctions)
                    {
                        AnalysisLoaclInvocation(item, components);
                    }
                }
                if (PropBindingsNode != null)
                {
                    var invctions = PropBindingsNode.Body.Descendants.OfType<InvocationExpression>();
                    foreach (var item in invctions)
                    {
                        if (item.Target.ToString().Contains("RegistMember"))
                        {
                            AnalysisBindingMembers(item, components);
                        }
                        else if (item.Target.ToString().Contains("RegistEvent"))
                        {
                            AnalysisBindingEvents(item, components);
                        }

                    }
                }
            }
        }
        /// <summary>
        /// 分析本地方法
        /// </summary>
        /// <param name="invocation"></param>
        /// <param name="components"></param>
        protected virtual void AnalysisLoaclInvocation(InvocationExpression invocation, List<ComponentItem> components)
        {
            var component = components.Find(x => invocation.Target.ToString().Contains("m_" + ));
            if (component != null)
            {
                string bindingSource = invocation.Arguments.First().ToString();
                var info = component.eventItems.Find(x => invocation.Target.ToString().Contains("m_" + component.name + "." + x.bindingTarget) && !x.runtime && x.bindingSource == bindingSource);

                if (info == null)
                {
                    var express = invocation.Target as MemberReferenceExpression;
                    var target = (express.Target as MemberReferenceExpression).MemberNameToken.Name;
                    Type infoType = GetTypeClamp(component.componentType, target);
                    info = new BindingEvent();
                    info.runtime = false;
                    info.bindingSource = bindingSource;
                    info.bindingTarget = target;
                    info.bindingTargetType.Update(infoType);
                    component.eventItems.Add(info);
                }
            }
        }
        /// <summary>
        /// 分析绑定member
        /// </summary>
        /// <param name="invocation"></param>
        /// <param name="components"></param>
        protected virtual void AnalysisBindingMembers(InvocationExpression invocation, List<ComponentItem> components)
        {
            var component = components.Find(x => invocation.Arguments.Count > 1 && invocation.Arguments.First().ToString().Contains("m_" + ));
            if (component != null)
            {
                var source = invocation.Arguments.ToArray()[1].ToString().Replace("\"", "");
                var info = component.viewItems.Find(x => x.bindingSource == source);
                if (info == null)
                {
                    info = new BindingShow();
                    info.bindingSource = source;
                    var arg0 = invocation.Arguments.First().ToString().Replace("\"", "");
                    var targetName = arg0.Substring(arg0.IndexOf(".") + 1);
                    var type = component.componentType.GetProperty(targetName);
                    info.bindingTarget = targetName;
                    info.bindingTargetType.Update(type.PropertyType);
                    component.viewItems.Add(info);
                }
            }
        }
        /// <summary>
        /// 分析绑定方法
        /// </summary>
        /// <param name="invocation"></param>
        /// <param name="components"></param>
        protected virtual void AnalysisBindingEvents(InvocationExpression invocation, List<ComponentItem> components)
        {
            var component = components.Find(x => invocation.Arguments.Count > 1 && invocation.Arguments.First().ToString().Contains("m_" + ));
            if (component != null)
            {
                var source = invocation.Arguments.ToArray()[1].ToString().Replace("\"", "");
                var info = component.eventItems.Find(x => x.bindingSource == source && x.runtime);
                if (info == null)
                {
                    info = new BindingEvent();
                    info.bindingSource = source;
                    var arg0 = invocation.Arguments.First().ToString();
                    var targetName = arg0.Substring(arg0.IndexOf(".") + 1);
                    Type infoType = GetTypeClamp(component.componentType, targetName);
                    info.runtime = true;
                    info.bindingTarget = targetName;
                    info.bindingTargetType.Update(infoType);
                    component.eventItems.Add(info);
                }
            }
        }
        protected Type GetTypeClamp(Type baseType, string membername)
        {
            Type infoType = null;
            var prop = baseType.GetProperty(membername);
            if (prop != null)
            {
                infoType = prop.PropertyType;
            }
            var field = baseType.GetField(membername);
            if (field != null)
            {
                infoType = field.FieldType;
            }
            return infoType;
        }
    }
}
```


如果你有自己的界面框架，那么你可以考虑将mvvm集成到其中，毕竟你不使用mvvm模式也是可以正常运行的。

        集成到框架中有一个关键，那就是view层的基类中，统一进行viewmodel的注册与注销。并指定viewmodel的传入方式，如果不动态传入那要它何用，既然要动态传入那么怎么解耦。我的框架中已经比较好的解决了这个问题，就算你不传入viewmodel也是可以设置绑定好的数据及事件的。前提是你将它们保存到一个IDictionary中。

        下面是本框架绑定的方式：

```C#
protected virtual void HandleData(object data)
        {
            if (data is Binding.ViewModelBase)
            {
                BindingContext = data as Binding.ViewModelBase;
            }
            else
            {
                if (data is IDictionary)
                {
                    LoadIDictionary(data as IDictionary);
                }
            }
        }
        private void LoadIDictionary(IDictionary data)
        {
            foreach (var item in data.Keys)
            {
                var key = item.ToString();
                var prop = BindingContext.GetBindableProperty(key, data[item].GetType());
                if (prop != null)
                {
                    prop.ValueBoxed = data[item];
                }
            }
        }
```
