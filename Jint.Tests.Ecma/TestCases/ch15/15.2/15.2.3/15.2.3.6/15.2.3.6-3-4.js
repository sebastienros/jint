/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * The abtract operation ToPropertyDescriptor  is used to package the
 * into a property desc. Step 10 of ToPropertyDescriptor throws a TypeError
 * if the property desc ends up having a mix of accessor and data property elements.
 *
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-4.js
 * @description Object.defineProperty throws TypeError if desc has 'set' and 'writable' present(8.10.5 step 9.a)
 */


function testcase() {
    var o = {};
    
    // dummy getter
    var setter = function () { }
    var desc = { set: setter, writable: false };
    
    try {
      Object.defineProperty(o, "foo", desc);
    }
    catch (e) {
      if (e instanceof TypeError &&
          (o.hasOwnProperty("foo") === false)) {
        return true;
      }
    }
 }
runTestCase(testcase);
