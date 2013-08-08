/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-4.js
 * @description Object.seal - 'O' is a Boolean object
 */


function testcase() {

        var boolObj = new Boolean(false);
        var preCheck = Object.isExtensible(boolObj);
        Object.seal(boolObj);

        return preCheck && Object.isSealed(boolObj);

    }
runTestCase(testcase);
