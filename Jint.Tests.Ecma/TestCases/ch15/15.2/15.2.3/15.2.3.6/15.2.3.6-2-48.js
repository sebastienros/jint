/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-48.js
 * @description Object.defineProperty - an inherited toString method  is invoked when 'P' is an object with an own valueOf and an inherited toString methods
 */


function testcase() {
        var obj = {};
        var toStringAccessed = false;
        var valueOfAccessed = false;

        var proto = {
            toString: function () {
                toStringAccessed = true;
                return "test";
            }
        };

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();
        child.valueOf = function () {
            valueOfAccessed = true;
            return "10";
        };

        Object.defineProperty(obj, child, {});

        return obj.hasOwnProperty("test") && !valueOfAccessed && toStringAccessed;
    }
runTestCase(testcase);
