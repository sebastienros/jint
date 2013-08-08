/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-13.js
 * @description Array.prototype.indexOf - 'length' is inherited accessor property without a get function
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
        child[1] = true;

        return Array.prototype.indexOf.call(child, true) === -1;
    }
runTestCase(testcase);
