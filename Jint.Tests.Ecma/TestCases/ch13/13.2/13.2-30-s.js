/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-30-s.js
 * @description StrictMode - property named 'caller' of function objects is not configurable
 * @onlyStrict
 */



function testcase() {
        return ! Object.getOwnPropertyDescriptor(Function("'use strict';"), 
                                                  "caller").configurable;
}
runTestCase(testcase);