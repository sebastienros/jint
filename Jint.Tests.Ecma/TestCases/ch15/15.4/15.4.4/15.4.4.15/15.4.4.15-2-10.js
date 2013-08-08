/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-10.js
 * @description Array.prototype.lastIndexOf - 'length' is inherited accessor property on an Array-like object
 */


function testcase() {

        var proto = {};
        Object.defineProperty(proto, "length", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        var Con = function () {};
        Con.prototype = proto;

        var child = new Con();
        child[1] = 1;
        child[2] = 2;

        return Array.prototype.lastIndexOf.call(child, 1) === 1 &&
            Array.prototype.lastIndexOf.call(child, 2) === -1;
    }
runTestCase(testcase);
