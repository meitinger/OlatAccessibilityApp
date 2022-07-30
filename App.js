/* Copyright (C) 2022, Manuel Meitinger
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

window.addEventListener('load', (e) => {
    'use strict'

    // get the top tool panel element
    const panel = document.getElementById('o_navbar_tools_permanent')
    if (!panel) return

    // helper function that creates an icon
    const icon = (code) => {
        if (typeof code !== 'number') throw new TypeError()
        const icon = document.createElement('i')
        icon.className = 'o_icon o_icon-fw'
        icon.textContent = String.fromCharCode(code)
        return icon
    }

    // helper function that creates and prepends a link to the panel
    const prependLink = (action) => {
        if (typeof action !== 'function') throw new TypeError()
        const li = document.createElement('li')
        li.className = 'o_navbar_tool'
        const a = document.createElement('a')
        a.href = 'javascript:;'
        a.onclick = action
        li.append(a)
        panel.prepend(li)
        return a
    }

    // helper function that gets an array containing all text sub-nodes
    const getChildTextNodes = (node) => {
        if (!(node instanceof Node)) throw new TypeError()
        switch (node.nodeType) {
            case Node.TEXT_NODE: return [node]
            case Node.ELEMENT_NODE: return node.nodeName.toLowerCase() === 'script' ? [] : [...node.childNodes].flatMap(getChildTextNodes)
            default: return []
        }
    }

    // helper function to notify the host application
    const postWebMessage = (name, args) => window.chrome.webview.postMessage({ name, ...args })

    // text-to-speech functions
    let ttsContext = null
    const ttsVoiceLabel = document.createElement('span')
    const ttsReserveIds = (() => {
        let nextId = 1
        return (count) => {
            if (typeof count !== 'number') throw new TypeError()
            if (count <= 0) throw new RangeError()
            const startId = nextId
            nextId += count
            return startId
        }
    })()
    const ttsInitialize = (() => {
        let initialized = false
        return () => {
            if (!initialized) {
                postWebMessage('ttsInitialize', { language: document.documentElement.getAttribute('lang') })
                initialized = true
            }
        }
    })()
    const [ttsSelectNode, ttsSelectWord, ttsClearSelection] = (() => {
        const selection = window.getSelection()
        let lastRange = null
        const clear = () => {
            if (lastRange !== null) {
                selection.removeRange(lastRange)
                lastRange.detach()
                lastRange = null
            }
        }
        const select = (id, selector) => {
            if (ttsContext === null || id < ttsContext.startId || id > ttsContext.startId + ttsContext.nodes.length) return
            clear()
            if (id === ttsContext.startId + ttsContext.nodes.length) return
            const range = document.createRange()
            selector(ttsContext.nodes[id - ttsContext.startId], range)
            selection.removeAllRanges() // alas, this is necessary
            selection.addRange(range)
            lastRange = range
        }
        const selectNode = (id) => {
            if (typeof id !== 'number') throw new TypeError()
            select(id, (node, range) => range.selectNodeContents(node))
        }
        const selectWord = (id, start, end) => {
            if (typeof id !== 'number' || typeof start !== 'number' || typeof end !== 'number') throw new TypeError()
            if (start < 0 || end < start) throw new RangeError()
            select(id, (node, range) => {
                range.setStart(node, start)
                range.setEnd(node, end)
            })
        }
        return [selectNode, selectWord, clear]
    })()
    const ttsCancel = () => {
        postWebMessage('ttsSpeakCancelAll')
        ttsClearSelection()
        if (ttsContext !== null) {
            clearTimeout(ttsContext.delay)
            ttsContext = null
        }
    }
    const ttsMouseMove = (e) => {
        // get the parent block element
        let target = e.target
        while (window.getComputedStyle(target).display.includes('inline')) target = target.parentElement

        // don't do anything if the block is already hovered, otherwise reset
        if (ttsContext?.target === target) return
        ttsCancel()

        // get the text nodes and ensure the cursor is over one of them
        const nodes = getChildTextNodes(target)
        if (!nodes.some(node => {
            const range = document.createRange()
            range.selectNodeContents(node)
            const rect = range.getBoundingClientRect()
            range.detach()
            return rect.left <= e.clientX && e.clientX <= rect.right && rect.top <= e.clientY && e.clientY <= rect.bottom
        })) return

        // select the first node and create the context
        const startId = ttsReserveIds(nodes.length)
        ttsContext = {
            target,
            startId,
            nodes,
            delay: setTimeout(() => {
                nodes.forEach((node, index) => postWebMessage('ttsSpeak', {
                    id: startId + index,
                    text: node.data,
                }))
                ttsSelectNode(startId)
            }, 500)
        }
    }

    // message handling
    window.chrome.webview.addEventListener('message', (e) => {
        const message = e.data
        switch (message.name) {
            case 'ttsInitialized':
                ttsVoiceLabel.textContent = message.voice
                break
            case 'ttsSpeakComplete':
                ttsSelectNode(message.id + 1)
                break
            case 'ttsSpeakProgress':
                ttsSelectWord(message.id, message.position, message.position + message.count)
                break
        }
    })

    // create the toggle-color link
    prependLink(() => {
        const colorRegex = /^rgb(a?)\((\d+), (\d+), (\d+)((, \d+)?)\)$/
        const invertColor = (color) => {
            const match = color.match(colorRegex)
            return match ? `rgb${match[1]}(${255 - match[2]}, ${255 - match[3]}, ${255 - match[4]}${match[5]})` : color
        }
        for (const styleSheet of document.styleSheets) {
            for (const rule of styleSheet.cssRules) {
                if (rule.style?.color) rule.style.color = invertColor(rule.style.color)
                if (rule.style?.backgroundColor) rule.style.backgroundColor = invertColor(rule.style.backgroundColor)
            }
        }
    }).append(icon(0xf042))

    // create three zoom links
    for (const zoom of [200, 150, 100]) {
        const link = prependLink(() => postWebMessage('zoom', { factor: zoom }))
        link.style.fontSize = `${zoom}%`
        if (zoom > 100) link.style.paddingLeft = '0'
        link.innerText = 'a'
    }

    // create the reader link
    prependLink((() => {
        let enabled = false
        return () => {
            if (enabled) {
                ttsCancel()
                document.removeEventListener('mousemove', ttsMouseMove)
                ttsVoiceLabel.style.display = ''
            }
            else {
                ttsInitialize()
                ttsVoiceLabel.style.display = 'inline'
                document.addEventListener('mousemove', ttsMouseMove)
            }
            enabled = !enabled
        }
    })()).append(icon(0xf29e), ttsVoiceLabel)
})
