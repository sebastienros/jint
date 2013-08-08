/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-4.js
 * @description Object.freeze - 'O' is a Boolean object
 */


function testcase() {
        var boolObj = new Boolean(false);

        Object.freeze(boolObj);

        return Object.isFrozen(boolObj);
    }
runTestCase(testcase);
