/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-29-s.js
 * @description StrictMode - property named 'caller' of function objects is not configurable
 * @onlyStrict
 */



function testcase() {
        function foo() {"use strict";}
        return ! Object.getOwnPropertyDescriptor(foo, 
                                                  "caller").configurable;
}
runTestCase(testcase);