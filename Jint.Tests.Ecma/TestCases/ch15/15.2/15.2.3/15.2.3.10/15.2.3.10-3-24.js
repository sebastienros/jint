/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-24.js
 * @description Object.preventExtensions - [[Extensible]]: false on a prototype doesn't prevent adding properties to an instance that inherits from that prototype
 */


function testcase() {
        var proto = {};
        var preCheck = Object.isExtensible(proto);
        Object.preventExtensions(proto);

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;
        var child = new ConstructFun();

        child.prop = 10;

        return preCheck && child.hasOwnProperty("prop");
    }
runTestCase(testcase);
