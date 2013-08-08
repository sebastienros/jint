/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-24.js
 * @description Object.isExtensible returns true if O is extensible and has a prototype that is extensible
 */


function testcase() {
       
        var proto = {};

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var obj = new ConstructFun();

        return Object.isExtensible(obj);
    }
runTestCase(testcase);
