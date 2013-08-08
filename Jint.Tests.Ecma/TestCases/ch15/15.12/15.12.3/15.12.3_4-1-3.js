/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3_4-1-3.js
 * @description JSON.stringify a indirectly circular object throws a error
 */


function testcase() {
  var obj = {p1: {p2: {}}};
  obj.p1.p2.prop = obj;
  try {
     JSON.stringify(obj);
     return false;  // should not reach here
     }
   catch (e) {return  true}
  }
runTestCase(testcase);
