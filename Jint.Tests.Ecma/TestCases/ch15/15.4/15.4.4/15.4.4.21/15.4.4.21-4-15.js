/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-15.js
 * @description Array.prototype.reduce - calling with no callbackfn is the same as passing undefined for callbackfn
 */


function testcase() {
        var obj = { 10: 10 };
        var lengthAccessed = false;
        var loopAccessed = false;

        Object.defineProperty(obj, "length", {
            get: function () {
                lengthAccessed = true;
                return 20;
            },
            configurable: true
        });

        Object.defineProperty(obj, "0", {
            get: function () {
                loopAccessed = true;
                return 10;
            },
            configurable: true
        });

        try {
            Array.prototype.reduce.call(obj);
            return false;
        } catch (ex) {
            return (ex instanceof TypeError) && lengthAccessed && !loopAccessed;
        }
    }
runTestCase(testcase);
