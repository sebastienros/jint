/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-13.js
 * @description Array.prototype.lastIndexOf - 'length' is inherited accessor property without a get function on an Array-like object
 */


function testcase() {

        var proto = {};
        Object.defineProperty(proto, "length", {
            set: function () { },
            configurable: true
        });

        var Con = function () {};
        Con.prototype = proto;

        var child = new Con();
        child[0] = true;

        return Array.prototype.lastIndexOf.call(child, true) === -1;
    }
runTestCase(testcase);
