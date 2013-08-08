/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-1.js
 * @description delete operator returns false when deleting a direct reference to a var
 */


function testcase() {
  var x = 1;

  // Now, deleting 'x' directly should fail;
  var d = delete x;
  if(d === false && x === 1)
    return true;
 }
runTestCase(testcase);
