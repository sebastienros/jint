/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-34-s.js
 * @description StrictMode - property named 'arguments' of function objects is not configurable
 * @onlyStrict
 */



function testcase() {
        return ! Object.getOwnPropertyDescriptor(Function("'use strict';"), 
                                                  "arguments").configurable;
}
runTestCase(testcase);