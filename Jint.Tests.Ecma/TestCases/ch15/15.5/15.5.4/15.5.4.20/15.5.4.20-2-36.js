/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-36.js
 * @description String.prototype.trim - 'this' is a Boolean Object that converts to a string
 */


function testcase() {
        return (String.prototype.trim.call(new Boolean(false)) === "false");
    }
runTestCase(testcase);
