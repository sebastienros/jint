/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-1.js
 * @description Object.freeze - extensible of 'O' is set as false even if 'O' has no own property
 */


function testcase() {
        var obj = {};

        Object.freeze(obj);

        return !Object.isExtensible(obj);
    }
runTestCase(testcase);
