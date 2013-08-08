/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-30.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (the global object)
 */


function testcase() {
        var proto = Object.getPrototypeOf(fnGlobalObject());

        return proto.isPrototypeOf(fnGlobalObject()) === true;
    }
runTestCase(testcase);
