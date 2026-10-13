---@meta
-- AUTO-GENERATED from analysis of codebase and assets - do not edit by hand.
-- Source: Assets/Code/KSP/ScriptUI/LuaUIMgr.cs

---@class LuaUIMgr
local LuaUIMgr = {}

---@param optionsValue? { title?: string, x?: number, y?: number, width?: number, height?: number, visible?: boolean }
---@return integer windowId
function LuaUIMgr.Window(optionsValue) end

---@param windowId integer
---@return boolean result
function LuaUIMgr.Close(windowId) end

---@param windowId integer
---@return boolean result
function LuaUIMgr.Show(windowId) end

---@param windowId integer
---@return boolean result
function LuaUIMgr.Hide(windowId) end

---@param windowId integer
---@param title string
---@return boolean result
function LuaUIMgr.SetTitle(windowId, title) end

---@param windowId integer
---@param text string
---@return integer controlId
function LuaUIMgr.Label(windowId, text) end

---@param windowId integer
---@param text string
---@param onClick? fun()
---@return integer controlId
function LuaUIMgr.Button(windowId, text, onClick) end

---@param windowId integer
---@param text string
---@param value boolean
---@param onChanged? fun(value: boolean)
---@return integer controlId
function LuaUIMgr.Checkbox(windowId, text, value, onChanged) end

---@param windowId integer
---@param label string
---@param value string
---@param onChanged? fun(value: string)
---@return integer controlId
function LuaUIMgr.TextField(windowId, label, value, onChanged) end

---@param windowId integer
---@param label string
---@param value number
---@param onChanged? fun(value: number)
---@return integer controlId
function LuaUIMgr.NumberField(windowId, label, value, onChanged) end

---@param windowId integer
---@param label string
---@param itemsValue any[]
---@param selectedIndex integer
---@param onChanged? fun(value: string, index: integer)
---@return integer controlId
function LuaUIMgr.Dropdown(windowId, label, itemsValue, selectedIndex, onChanged) end

---@param windowId integer
---@param label string
---@param min number
---@param max number
---@param value number
---@param onChanged? fun(value: number)
---@return integer controlId
function LuaUIMgr.Slider(windowId, label, min, max, value, onChanged) end

---@param windowId integer
---@return integer controlId
function LuaUIMgr.Separator(windowId) end

---@param windowId integer
---@param controlId integer
---@param text string
---@return boolean result
function LuaUIMgr.SetText(windowId, controlId, text) end

---@param windowId integer
---@param controlId integer
---@param value any
---@return boolean result
function LuaUIMgr.SetValue(windowId, controlId, value) end

---@param windowId integer
---@param controlId integer
---@param enabled boolean
---@return boolean result
function LuaUIMgr.SetEnabled(windowId, controlId, enabled) end

---@param windowId integer
---@param controlId integer
---@return boolean result
function LuaUIMgr.Remove(windowId, controlId) end

---@param title string
---@param message string
---@param onClose? fun()
---@return integer dialogId
function LuaUIMgr.Dialog(title, message, onClose) end

---@param title string
---@param message string
---@param onClose? fun()
---@return integer dialogId
function LuaUIMgr.Alert(title, message, onClose) end

---@param title string
---@param message string
---@param onConfirm? fun()
---@param onCancel? fun()
---@return integer dialogId
function LuaUIMgr.Confirm(title, message, onConfirm, onCancel) end

---@param dialogId integer
---@return boolean result
function LuaUIMgr.CloseDialog(dialogId) end
