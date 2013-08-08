/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-47.js
 * @description Object.getOwnPropertyDescriptor - uses inherited toString method when 'P' is an object with an own valueOf and inherited toString methods
 */


function testcase() {
        var proto = {};
        var valueOfAccessed = false;
        var toStringAccessed = false;

        proto.toString = function () {
            toStringAccessed = true;
            return "test";
        };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.valueOf = function () {
            valueOfAccessed = true;
            return "10";
        };
        var obj = { "10": "length1", "test": "length2" };
        var desc = Object.getOwnPropertyDescriptor(obj, child);

        return desc.value === "length2" && toStringAccessed && !valueOfAccessed;
    }
runTestCase(testcase);
