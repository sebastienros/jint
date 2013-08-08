/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-15.js
 * @description Array.prototype.some - element to be retrieved is inherited accessor property on an Array-like object
 */


function testcase() {

        var kValue = "abc";

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                return val === kValue;
            }
            return false;
        }

        var proto = {};

        Object.defineProperty(proto, "1", {
            get: function () {
                return kValue;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 20;

        return Array.prototype.some.call(child, callbackfn);
    }
runTestCase(testcase);
