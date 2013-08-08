/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-15.js
 * @description Object.getOwnPropertyDescriptor applied to a Function object which implements its own property get method
 */


function testcase() {

        var obj = function (a, b) {
            return a + b;
        };
        obj[1] = "ownProperty";

        var desc = Object.getOwnPropertyDescriptor(obj, "1");

        return desc.value === "ownProperty";
    }
runTestCase(testcase);
