// original code: https://github.com/thecodejunkie/unity.resources/blob/master/scripts/editor/ExtendedEditorWindow.cs

using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace Merino
{
	internal static class MouseButton
	{
		public const int Left = 0;
		public const int Right = 1;
		public const int Middle = 2;
	}

	public class EventWindow : EditorWindow
	{
		protected delegate void EventAction(Event e);
		protected Dictionary<EventType, EventAction> eventTable;
		protected Dictionary<EventType, EventAction> rawEventTable;
		
		public EventWindow()
		{
			eventTable = new Dictionary<EventType, EventAction> {
				{ EventType.MouseDown,       e => OnMouseDown(e)       },
				{ EventType.MouseUp,         e => OnMouseUp(e)         },
				{ EventType.MouseDrag,       e => OnMouseDrag(e)       },
				{ EventType.MouseMove,       e => OnMouseMove(e)       },
				{ EventType.ScrollWheel,     e => OnScrollWheel(e)     },
				{ EventType.ContextClick,    e => OnContextClick(e)    },
				{ EventType.KeyDown,         e => OnKeyDown(e)         },
				{ EventType.KeyUp,           e => OnKeyUp(e)           },
				{ EventType.ValidateCommand, e => OnValidateCommand(e) },
				{ EventType.ExecuteCommand,  e => OnExecuteCommand(e)  },
			};
			rawEventTable = new Dictionary<EventType, EventAction> {
				{ EventType.MouseDown,       e => OnRawMouseDown(e)    },
				{ EventType.MouseUp,         e => OnRawMouseUp(e)      },
				{ EventType.MouseDrag,       e => OnRawMouseDrag(e)    },
				{ EventType.MouseMove,       e => OnRawMouseMove(e)    },				
			};
		}
		
		protected virtual void OnMouseDown(Event e) { }
		protected virtual void OnMouseUp(Event e)   { }
		protected virtual void OnMouseDrag(Event e) { }
		protected virtual void OnMouseMove(Event e) { }
		protected virtual void OnScrollWheel(Event e) { }
		protected virtual void OnContextClick(Event e) { }
		protected virtual void OnKeyDown(Event e) { }
		protected virtual void OnKeyUp(Event e) { }
		protected virtual void OnValidateCommand(Event e) { }
		protected virtual void OnExecuteCommand(Event e) { }

		protected virtual void OnRawMouseDown(Event e) { }		
		protected virtual void OnRawMouseUp(Event e)   { }
		protected virtual void OnRawMouseDrag(Event e) { }
		protected virtual void OnRawMouseMove(Event e) { }
		
		protected virtual void HandleEvents(Event e)
		{
			EventAction handler;
			if (rawEventTable.TryGetValue(e.rawType, out handler))
			{
				handler.Invoke(e);
			}
			if (eventTable.TryGetValue(e.type, out handler))
			{
				handler.Invoke(e);
			}
		}

		/// <summary>
		/// Gets if the EditorWindow docked.
		/// </summary>
		protected bool IsDocked()
		{
			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var isDockedMethod = typeof(EditorWindow).GetProperty("docked", flags).GetGetMethod(true);
			return (bool) isDockedMethod.Invoke(this, null);
		}

		/// <summary>
		/// Gets if the EditorWindow docked.
		/// This version delays reporting the docked value since it will occasionally not report properly otherwise.
		/// </summary>
		protected void IsDocked_Delayed(Action<bool> callback)
		{
			EditorApplication.delayCall += () => 
			{
				var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
				var isDockedMethod = typeof(EditorWindow).GetProperty("docked", flags).GetGetMethod(true);
				if (callback != null)
					callback((bool) isDockedMethod.Invoke(this, null));
			};
		}
	}
}
