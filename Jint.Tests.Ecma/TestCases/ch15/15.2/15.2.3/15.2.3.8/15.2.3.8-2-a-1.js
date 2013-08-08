/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-1.js
 * @description Object.seal - 'P' is own data property
 */


function testcase() {
        var obj = {};

        obj.foo = 10; // default [[Configurable]] attribute value of foo: true
        var preCheck = Object.isExtensible(obj);
        Object.seal(obj);

        delete obj.foo;
        return preCheck && obj.foo === 10;
    }
runTestCase(testcase);
