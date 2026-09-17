#Requires AutoHotkey v2.0
#SingleInstance Force

intervalMs := Round(1000 / 30)

F1::{
    while GetKeyState("F1", "P") {
        Send "{Blind}{a down}{d down}{Space down}"
        Sleep 1
        Send "{Blind}{a up}{d up}{Space up}"
        Sleep Max(1, intervalMs - 1)
    }
}
