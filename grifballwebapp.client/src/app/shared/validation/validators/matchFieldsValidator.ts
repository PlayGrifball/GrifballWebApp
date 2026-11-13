import { AbstractControl, FormGroup, ValidationErrors, ValidatorFn } from "@angular/forms";

export function matchFieldsValidator(field1: AbstractControl, field2: AbstractControl): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const forbidden = field2.value !== field1.value;
    return forbidden ? { matchOtherValidator: { otherControlName: getName(field1) } } : null;
  };
}

function getName(control: AbstractControl): string | null {
  let group = <FormGroup>control.parent;

  if (!group) {
    return null;
  }

  let name: string | null = null;

  Object.keys(group.controls).forEach(key => {
    let childControl = group.get(key);

    if (childControl !== control) {
      return;
    }

    name = key;
  });

  return name;
}
