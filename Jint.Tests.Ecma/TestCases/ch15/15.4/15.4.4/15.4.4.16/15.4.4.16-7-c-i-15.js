/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-15.js
 * @description Array.prototype.every - element to be retrieved is inherited accessor property on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                return val !== 11;
            } else {
                return true;
            }
        }

        var proto = {};

        Object.defineProperty(proto, "1", {
            get: function () {
                return 11;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 20;

        return !Array.prototype.every.call(child, callbackfn);
    }
runTestCase(testcase);
