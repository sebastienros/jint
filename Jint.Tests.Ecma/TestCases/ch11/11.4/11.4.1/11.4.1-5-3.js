/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-3.js
 * @description delete operator returns false when deleting a direct reference to a function name
 */


function testcase() {
  var foo = function(){};

  // Now, deleting 'foo' directly should fail;
  var d = delete foo;
  if(d === false && fnExists(foo))
    return true;
 }
runTestCase(testcase);
