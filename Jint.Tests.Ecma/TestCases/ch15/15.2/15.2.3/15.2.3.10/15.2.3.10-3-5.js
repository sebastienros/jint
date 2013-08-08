/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-5.js
 * @description Object.preventExtensions - indexed properties cannot be added into a String object
 */


function testcase() {
        var strObj = new String();
        var preCheck = Object.isExtensible(strObj);
        Object.preventExtensions(strObj);
        try {
            Object.defineProperty(strObj, "0", { value: "c" });
            return false;
        } catch (e) {
            return e instanceof TypeError && preCheck &&
                !strObj.hasOwnProperty("0") && typeof strObj[0] === "undefined";
        }
    }
runTestCase(testcase);
