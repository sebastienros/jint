/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-12.js
 * @description Object.freeze - 'P' is own index property of a String object that implements its own [[GetOwnProperty]]
 */


function testcase() {

        // default [[Configurable]] attribute value of "0": true
        var strObj = new String("abc");

        Object.freeze(strObj);

        var desc = Object.getOwnPropertyDescriptor(strObj, "0");

        delete strObj[0];
        return strObj[0] === "a" && desc.configurable === false && desc.writable === false;
    }
runTestCase(testcase);
