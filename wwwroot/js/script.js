function detectKeyboard() {
    var isKeyboardVisible = window.innerHeight < screen.height * 0.85;
    DotNet.invokeMethodAsync('Manta', 'KeyboardChanged', isKeyboardVisible);
}

window.addEventListener('resize', detectKeyboard);