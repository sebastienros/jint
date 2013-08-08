/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-1.js
 * @description Object.seal - extensible of 'O' is set as false even if 'O' has no own property
 */


function testcase() {
        var obj = {};

        var preCheck = Object.isExtensible(obj);

        Object.seal(obj);

        return preCheck && !Object.isExtensible(obj);
    }
runTestCase(testcase);
