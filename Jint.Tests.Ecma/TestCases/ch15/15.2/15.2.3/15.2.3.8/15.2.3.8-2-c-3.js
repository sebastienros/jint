/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-3.js
 * @description Object.seal - 'O' is a String object
 */


function testcase() {

        var strObj = new String("a");
        var preCheck = Object.isExtensible(strObj);
        Object.seal(strObj);

        return preCheck && Object.isSealed(strObj);

    }
runTestCase(testcase);
