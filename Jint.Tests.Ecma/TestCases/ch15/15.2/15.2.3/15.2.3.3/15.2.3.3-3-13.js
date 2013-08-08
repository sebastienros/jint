/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-13.js
 * @description Object.getOwnPropertyDescriptor applied to the Arguments object which implements its own property get method
 */


function testcase() {

        var arg = (function () {
            return arguments;
        }("ownProperty", true));

        var desc = Object.getOwnPropertyDescriptor(arg, "0");

        return desc.value === "ownProperty" && desc.writable === true && desc.enumerable === true && desc.configurable === true;
    }
runTestCase(testcase);
