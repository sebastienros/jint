/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-13.js
 * @description Array.prototype.filter applied to the Array-like object that 'length' is inherited accessor property without a get function
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return true;
        }

        var proto = {};
        Object.defineProperty(proto, "length", {
            set: function () { },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child[0] = 11;
        child[1] = 12;

        var newArr = Array.prototype.filter.call(child, callbackfn);
        return newArr.length === 0 && !accessed;
    }
runTestCase(testcase);
