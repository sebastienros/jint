// All four custom-element reactions: the upgrade of an element the parser already made, connected and
// disconnected as it moves through the tree, and an observed attribute changing.
(function () {
  'use strict';

  var reactions = [];

  class MyCounter extends HTMLElement {
    static get observedAttributes() { return ['start']; }

    constructor() {
      super();
      reactions.push('constructed');
    }

    connectedCallback() {
      reactions.push('connected:' + (this.id || 'anonymous'));
      this.render();
      this.addEventListener('click', () => {
        this.setAttribute('start', String(Number(this.getAttribute('start') || '0') + 1));
      });
    }

    disconnectedCallback() {
      reactions.push('disconnected:' + (this.id || 'anonymous'));
    }

    attributeChangedCallback(name, oldValue, newValue) {
      reactions.push('attribute:' + name + ':' + oldValue + '->' + newValue);
      this.render();
    }

    render() {
      this.textContent = 'count ' + (this.getAttribute('start') || '0');
    }
  }

  customElements.define('my-counter', MyCounter);

  window.__reactions = function () { return reactions.join('|'); };

  window.__addOne = function () {
    var made = document.createElement('my-counter');
    made.id = 'second';
    made.setAttribute('start', '10');
    document.getElementById('host').appendChild(made);
  };

  window.__removeOne = function () {
    document.getElementById('second').remove();
  };

  customElements.whenDefined('my-counter').then(function () {
    document.getElementById('reactions').textContent = 'defined';
  });
})();
