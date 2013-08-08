/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-11.js
 * @description Object.freeze - 'P' is own index property of the Arguments object that implements its own [[GetOwnProperty]]
 */


function testcase() {

        // default [[Configurable]] attribute value of "0": true
        var argObj = (function () { return arguments; }(1, 2, 3));

        Object.freeze(argObj);

        var desc = Object.getOwnPropertyDescriptor(argObj, "0");

        delete argObj[0];
        return argObj[0] === 1 && desc.configurable === false && desc.writable === false;
    }
runTestCase(testcase);
