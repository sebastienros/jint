/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.1/15.1.1/15.1.1.3/15.1.1.3-2.js
 * @description undefined is not writable, should throw TypeError in strict mode
 * @onlyStrict
 */

function testcase(){
  "use strict";
  var global = fnGlobalObject();
  try{
    global["undefined"] = 5;  // Should throw a TypeError as per 8.12.5
  } catch (ex) {
    if(ex instanceof TypeError){
      return true;
    } else {
      return false;
    }
  }
}

runTestCase(testcase);
