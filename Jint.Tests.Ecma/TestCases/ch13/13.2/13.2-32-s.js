/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-32-s.js
 * @description StrictMode - property named 'caller' of function objects is not configurable
 * @onlyStrict
 */



function testcase() {
        var funcExpr = function () { "use strict";};
        return ! Object.getOwnPropertyDescriptor(funcExpr, 
                                                  "caller").configurable;
}
runTestCase(testcase);